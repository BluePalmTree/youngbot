using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Controls;
using chess_engine.Models;
using chess_ui.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;

namespace chess_ui.ViewModels
{
    public partial class BoardViewModel : ViewModelBase
    {
        private const byte BoardSize = 8;
        private const string StartPosition = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
        private Board _board;

        public string[] Ranks { get; } // rows        
        public string[] Files { get; } // columns

        public BoardViewModel()
        {
            Ranks = ["8", "7", "6", "5", "4", "3", "2", "1"];
            Files = ["a", "b", "c", "d", "e", "f", "g", "h"];

            // Init squares
            var sq = new SquareViewModel[64];
            byte i = 0;
            for (int r = 0; r < BoardSize; r++)
            {
                for (int c = 0; c < BoardSize; c++)
                {
                    sq[i] = new SquareViewModel(i, (r + c) % 2 == 0);
                    i++;
                }
            }
            _squares = new ObservableCollection<SquareViewModel>(sq);

            _board = Board.FromStartPosition(StartPosition);
            SyncFromBoard();
        }

        [ObservableProperty]
        private ObservableCollection<SquareViewModel> _squares;






        public void MovePiece(int fromIndex, int toIndex)
        {
            foreach (var sq in Squares)
                sq.IsHighlighted = false;

            var from = Squares[fromIndex];
            var to = Squares[toIndex];

            to.Piece = from.Piece;
            from.Piece = null;
            from.IsGhost = false;
            from.IsSelected = false;

            from.IsHighlighted = true;
            to.IsHighlighted = true;
        }





        private void SyncFromBoard()
        {
            for (int rank = 0; rank < BoardSize; rank++)
            {
                for (int file = 0; file < BoardSize; file++)
                {
                    int engineIndex = Board.IndexOf(file, rank);
                    int uiIndex = Board.ToUiIndex(rank, file);
                    Debug.WriteLine($"Rank: {8 - rank}/{rank} File: {(char)('a' + file)}/{file} EngineIndex: {engineIndex:00} UiIndex: {uiIndex:00} Code: {PieceCodeMapper.ToCode(_board.Squares[engineIndex])} Piece: {_board.Squares[engineIndex]:b}");
                    Squares[uiIndex].Piece = PieceCodeMapper.ToCode(_board.Squares[engineIndex]);
                    Squares[uiIndex].EngineIndex = engineIndex;
                }
            }
        }
    }
}