using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using chess_engine.Bots;
using chess_engine.Engine;
using chess_engine.Helpers;
using chess_engine.Models;
using chess_ui.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace chess_ui.ViewModels
{
    public partial class BoardViewModel : ViewModelBase
    {
        //private const string StartPosition = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1"; // default
        private const string StartPosition = "r3k3/1p3p2/p2q2p1/bn3P2/1N2PQP1/PB6/3K1R1r/3R4 w - - 0 1"; // seb



        private const byte BoardSize = 8;
        private Board _board;
        private Move? _pendingPromotionMove;

        public string[] Ranks { get; } // rows        
        public string[] Files { get; } // columns

        // Raised when promotion is needed; int = UI index of the target square
        public event Action<int>? PromotionRequired;
        public event Action? PromotionCompleted;

        public BoardViewModel()
        {
            Ranks = ["8", "7", "6", "5", "4", "3", "2", "1"];
            Files = ["a", "b", "c", "d", "e", "f", "g", "h"];

            var sq = new SquareViewModel[BoardSize * BoardSize];
            var i = 0;
            for (int r = 0; r < BoardSize; r++)
            {
                for (int f = 0; f < BoardSize; f++)
                {
                    sq[i] = new SquareViewModel(i, (r + f) % 2 == 0);
                    i++;
                }
            }
            Squares = new ObservableCollection<SquareViewModel>(sq);
            _board = new Board();
            AttackedSquares = 0UL;
            PinnedSquares = 0UL;
            CheckSquares = 0UL;

            NewGame();
        }

        #region Observable Properties

        [ObservableProperty]
        private ObservableCollection<SquareViewModel> _squares;

        [ObservableProperty]
        private int _fullMoveNumber;

        [ObservableProperty]
        private int _halfMoveClock;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(UndoMoveCommand))]
        private int _gameStatesCount;

        [ObservableProperty]
        private int _castlingRights;

        [ObservableProperty]
        private int _enPassantSquare;

        [ObservableProperty]
        private GameStatus _gameStatus = GameStatus.Playing;

        [ObservableProperty]
        private string _gameOverMessage = string.Empty;

        [ObservableProperty]
        private bool _isBlackBot = false;

        [ObservableProperty]
        private bool _isWhiteBot;

        [ObservableProperty]
        private ulong _attackedSquares;

        [ObservableProperty]
        private ulong _pinnedSquares;

        [ObservableProperty]
        private ulong _checkSquares;

        [ObservableProperty]
        private bool _highlightAttackedSquares = false;

        [ObservableProperty]
        private bool _highlightPinnedSquares = false;

        [ObservableProperty]
        private bool _highlightCheckSquares = false;

        #endregion

        #region Commands

        [RelayCommand]
        private void NewGame()
        {
            _board = Board.FromStartPosition(StartPosition);
            GameStatus = GameStatus.Playing;
            CastlingRights = _board.CastlingRights;
            EnPassantSquare = _board.EnPassantSquare;
            FullMoveNumber = _board.FullMoveNumber;
            HalfMoveClock = _board.HalfMoveClock;
            GameStatesCount = _board.GameStates.Count;
            AttackedSquares = _board.AttackData?.AttackMap ?? 0UL;
            PinnedSquares = _board.AttackData is null ? 0 : BitUtils.OrListTogether(_board.AttackData.PinLines.Values);
            CheckSquares = _board.AttackData?.CheckBlockMask ?? 0UL;

            foreach (var sq in Squares)
            {
                sq.IsHighlighted = false;
                sq.IsValidMoveTarget = false;
                sq.IsSelected = false;
                sq.IsGhost = false;
            }

            SyncFromBoard();
            MakeBotMoveIfNeeded();
        }

        [RelayCommand(CanExecute = nameof(CanUndoMove))]
        private void UndoMove()
        {
            _board.UnmakeLastMove();
            MoveGenerator.GenerateLegalMoves(_board);
            SyncFromBoard();
            GameStatesCount = _board.GameStates.Count;
            FullMoveNumber = _board.FullMoveNumber;
            HalfMoveClock = _board.HalfMoveClock;
            CastlingRights = _board.CastlingRights;
            EnPassantSquare = _board.EnPassantSquare;
            AttackedSquares = _board.AttackData?.AttackMap ?? 0UL;
            PinnedSquares = _board.AttackData is null ? 0 : BitUtils.OrListTogether(_board.AttackData.PinLines.Values);
            CheckSquares = _board.AttackData?.CheckBlockMask ?? 0UL;

            foreach (var sq in Squares)
            {
                sq.IsHighlighted = false;
                sq.IsValidMoveTarget = false;
            }

            MakeBotMoveIfNeeded();
        }

        [RelayCommand]
        private void RunPerft(object? parameter)
        {
            if (parameter is not string s)
                return;

            var parts = s.Split(' ');
            if (parts.Length != 2)
                return;

            if (!int.TryParse(parts[1], out int depth))
                return;

            string fen = parts[0] switch
            {
                "start" => "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",
                "kiwipete" => "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1",
                "position3" => "8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1",
                _ => StartPosition,
            };

            Perft.Divide(parts[0], fen, depth);
        }

        #endregion

        #region Public Methods

        public void SelectSquare(int index)
        {
            var square = Squares[index];
            square.IsSelected = true;


            int[] movesForSquare = MoveGenerator.GetValidMovesForSquare(square.EngineIndex);
            //Debug.WriteLine($"Moves for selected square: {string.Join(", ", movesForSquare)}");
            for (int i = 0; i < 64; i++)
            {
                Squares[Board.ToUiIndex(i)].IsValidMoveTarget = false;

                if (movesForSquare.Contains(i))
                    Squares[Board.ToUiIndex(i)].IsValidMoveTarget = true;
            }
        }

        public bool TryMovePiece(int fromIndex, int toIndex)
        {
            int engineFrom = Board.ToEngineIndex(fromIndex);
            int engineTo = Board.ToEngineIndex(toIndex);

            var move = MoveGenerator.GetMove(engineFrom, engineTo);
            if (!move.HasValue)
                return false;

            // Check for promotion
            int piece = _board.Squares[engineFrom];
            bool isPromotion = Piece.TypeOf(piece) == Piece.Pawn
                               && (Board.RankOf(engineTo) == 7 || Board.RankOf(engineTo) == 0);

            if (isPromotion)
            {
                _pendingPromotionMove = move;
                PromotionRequired?.Invoke(toIndex); // tell the view which square to anchor to
                return true;
            }

            CompleteMove(move.Value);
            return true;
        }

        public void CompletePromotion(MoveFlag flag)
        {
            if (!_pendingPromotionMove.HasValue)
                throw new NullReferenceException("Pending promotion move was null");

            var promotionMove = new Move(_pendingPromotionMove.Value.From, _pendingPromotionMove.Value.To, flag);
            CompleteMove(promotionMove);
            PromotionCompleted?.Invoke();
        }

        #endregion

        #region Private Methods

        private bool CanUndoMove() => _board.GameStates.Count > 0;

        private void CompleteMove(Move move)
        {
            foreach (var sq in Squares)
            {
                sq.IsHighlighted = false;
                sq.IsValidMoveTarget = false;
            }

            var from = Squares[Board.ToUiIndex(move.From)];
            var to = Squares[Board.ToUiIndex(move.To)];

            from.IsGhost = false;
            from.IsSelected = false;
            from.IsHighlighted = true;
            to.IsHighlighted = true;

            _board.MakeMove(move, true);
            MoveGenerator.GenerateLegalMoves(_board);
            SyncFromBoard();
            GameStatesCount = _board.GameStates.Count;
            FullMoveNumber = _board.FullMoveNumber;
            HalfMoveClock = _board.HalfMoveClock;
            CastlingRights = _board.CastlingRights;
            EnPassantSquare = _board.EnPassantSquare;
            AttackedSquares = _board.AttackData?.AttackMap ?? 0UL;
            PinnedSquares = _board.AttackData is null ? 0 : BitUtils.OrListTogether(_board.AttackData.PinLines.Values);
            CheckSquares = _board.AttackData?.CheckBlockMask ?? 0UL;

            if (HalfMoveClock >= 50)
            {
                SetGameOver();
                return;
            }

            MakeBotMoveIfNeeded();
        }

        private void MakeBotMoveIfNeeded()
        {
            bool isCurrentColorBot = (_board.ColorToMove == Piece.White && IsWhiteBot)
                           || (_board.ColorToMove == Piece.Black && IsBlackBot);

            if (!isCurrentColorBot)
                return;

            if (GameStatus != GameStatus.Playing)
                return;

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var move = RandomBot.PickMove();

                if (move is null)
                {
                    // No legal moves — checkmate or stalemate
                    Debug.WriteLine("Game over: no legal moves for bot.");
                    SetGameOver();
                    return;
                }

                CompleteMove(move.Value);

            }, Avalonia.Threading.DispatcherPriority.Background);
        }

        private void SetGameOver()
        {
            bool isWhiteTurn = _board.ColorToMove == Piece.White;
            string side = isWhiteTurn ? "White has" : "Black has";

            GameStatus = GameStatus.Stalemate;
            GameOverMessage = $"Stalemate — {side} no legal moves.";

            if (HalfMoveClock >= 50)
            {
                GameOverMessage = "Fifty-Move rule broken";
            }
            else if (_board.IsInCheck())
            {
                GameStatus = GameStatus.Checkmate;
                GameOverMessage = $"Checkmate - {side} no legal moves";
            }
        }

        private void SyncFromBoard()
        {
            for (int rank = 0; rank < BoardSize; rank++)
            {
                for (int file = 0; file < BoardSize; file++)
                {
                    int engineIndex = Board.IndexOf(file, rank);
                    int uiIndex = Board.ToUiIndex(rank, file);
                    //Debug.WriteLine($"Rank: {8 - rank}/{rank} File: {(char)('a' + file)}/{file} EngineIndex: {engineIndex:00} UiIndex: {uiIndex:00} Code: {PieceCodeMapper.ToCode(_board.Squares[engineIndex])} Piece: {_board.Squares[engineIndex]:b}");
                    Squares[uiIndex].Piece = PieceCodeMapper.ToCode(_board.Squares[engineIndex]);
                    Squares[uiIndex].EngineIndex = engineIndex;
                }
            }
        }

        #endregion
    }
}