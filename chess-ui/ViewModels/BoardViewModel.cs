using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using chess_engine.Helpers;
using chess_engine.Models;
using chess_ui.Helpers;
using chess_ui.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace chess_ui.ViewModels
{
    public partial class BoardViewModel : ViewModelBase
    {
        private const byte BoardSize = 8;
        private const string StartPosition = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
        private Board _board;
        private int _pendingPromotionFrom;
        private int _pendingPromotionTo;

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
            byte i = 0;
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
            SyncFromBoard();
        }

        [ObservableProperty]
        private ObservableCollection<SquareViewModel> _squares;

        [ObservableProperty]
        private int _fullMoveNumber;

        public bool TryMovePiece(int fromIndex, int toIndex)
        {
            if (!MoveGenerator.IsValidMove(Board.ToEngineIndex(fromIndex), Board.ToEngineIndex(toIndex)))
                return false;

            // Check for promotion
            int engineTo = Board.ToEngineIndex(toIndex);
            int piece = _board.Squares[Board.ToEngineIndex(fromIndex)];
            bool isPromotion = Piece.TypeOf(piece) == Piece.Pawn
                && (Board.RankOf(engineTo) == 7 || Board.RankOf(engineTo) == 0);

            if (isPromotion)
            {
                _pendingPromotionFrom = fromIndex;
                _pendingPromotionTo = toIndex;
                PromotionRequired?.Invoke(toIndex); // tell the view which square to anchor to
                return true;
            }

            CompleteMove(fromIndex, toIndex, MoveFlag.Normal);
            return true;
        }

        public void SelectSquare(int index)
        {
            var square = Squares[index];
            square.IsSelected = true;


            int[] movesForSquare = MoveGenerator.GetValidMovesForSquare(square.EngineIndex);
            Debug.WriteLine($"Moves for selected square: {string.Join(", ", movesForSquare)}");
            for (int i = 0; i < 64; i++)
            {
                Squares[Board.ToUiIndex(i)].IsValidMoveTarget = false;

                if (movesForSquare.Contains(i))
                    Squares[Board.ToUiIndex(i)].IsValidMoveTarget = true;
            }
        }

        public void CompletePromotion(MoveFlag flag)
        {
            CompleteMove(_pendingPromotionFrom, _pendingPromotionTo, flag);
            PromotionCompleted?.Invoke();
        }

        private void CompleteMove(int fromIndex, int toIndex, MoveFlag flag)
        {
            foreach (var sq in Squares)
            {
                sq.IsHighlighted = false;
                sq.IsValidMoveTarget = false;
            }

            var from = Squares[fromIndex];
            var to = Squares[toIndex];

            from.IsGhost = false;
            from.IsSelected = false;
            from.IsHighlighted = true;
            to.IsHighlighted = true;

            _board = Board.Update(_board, from.EngineIndex, to.EngineIndex, flag);
            SyncFromBoard();
            FullMoveNumber = _board.FullMoveNumber;
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
    }
}