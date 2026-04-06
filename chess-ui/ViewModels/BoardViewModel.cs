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
                    sq[i] = new SquareViewModel(i, Board.IndexOf(c, r), (r + c) % 2 == 0);
                    i++;
                }
            }
            _squares = new ObservableCollection<SquareViewModel>(sq);

            _board = Board.DefaultStartPosition();
            SyncFromBoard();
        }

        [ObservableProperty]
        private ObservableCollection<SquareViewModel> _squares;






        public void MovePiece(int fromIndex, int toIndex)
        {
            var from = Squares[fromIndex];
            var to = Squares[toIndex];

            to.Piece = from.Piece;
            from.Piece = null;
            from.IsGhost = false;
        }





        private void SyncFromBoard()
        {
            for (int rank = 0; rank < BoardSize; rank++)
            {
                for (int file = 0; file < BoardSize; file++)
                {
                    int boardIndex = Board.IndexOf(file, rank);
                    int uiIndex = (7 - rank) * 8 + file;
                    Debug.WriteLine($"Rank: {rank:00} File: {file:00} BoardIndex: {boardIndex:00} UiIndex: {uiIndex:00} Code: {PieceCodeMapper.ToCode(_board.Squares[boardIndex])} Piece: {_board.Squares[boardIndex]:b}");
                    Squares[uiIndex].Piece = PieceCodeMapper.ToCode(_board.Squares[boardIndex]);
                    Squares[uiIndex].BoardIndex = boardIndex;
                }
            }
        }
    }
}