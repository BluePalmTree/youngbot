using System;
using System.Collections.ObjectModel;
using System.Linq;
using chess_engine.Helpers;
using chess_engine.Models;
using chess_ui.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace chess_ui.ViewModels
{
    public partial class BoardViewModel : ViewModelBase
    {
        private const byte BoardSize = 8;
        private const string StartPosition = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
        private readonly Board _board;
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

            // Init squares
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
            _squares = new ObservableCollection<SquareViewModel>(sq);

            _board = Board.FromStartPosition(StartPosition);
            CastlingRights = _board.CastlingRights;
            EnPassantSquare = _board.EnPassantSquare;
            SyncFromBoard();
        }

        [ObservableProperty]
        private ObservableCollection<SquareViewModel> _squares;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(UndoMoveCommand))]
        private int _fullMoveNumber;

        [ObservableProperty]
        private int _castlingRights;

        [ObservableProperty]
        private int _enPassantSquare;

        [RelayCommand(CanExecute = nameof(CanUndoMove))]
        private void UndoMove()
        {
            _board.UnmakeLastMove();
            MoveGenerator.GenerateMoves(_board);
            SyncFromBoard();
            FullMoveNumber = _board.GameStates.Count;
            CastlingRights = _board.CastlingRights;
            EnPassantSquare = _board.EnPassantSquare;

            foreach (var sq in Squares)
            {
                sq.IsHighlighted = false;
                sq.IsValidMoveTarget = false;
            }
        }

        [RelayCommand]
        private void RunPerft(object? parameter)
        {
            if (parameter is not null && int.TryParse(parameter.ToString(), out int depth))
                Perft.Divide(_board, depth);
        }


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

            _board.MakeMove(move);
            MoveGenerator.GenerateMoves(_board);
            SyncFromBoard();
            FullMoveNumber = _board.GameStates.Count;
            CastlingRights = _board.CastlingRights;
            EnPassantSquare = _board.EnPassantSquare;
        }


        private bool CanUndoMove() => _board.GameStates.Count > 0;
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
    }
}