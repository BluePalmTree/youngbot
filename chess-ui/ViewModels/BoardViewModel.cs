using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace chess_ui.ViewModels
{
    public partial class BoardViewModel : ViewModelBase
    {
        private const byte BoardSize = 8;

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
                    string? piece;
                    if (r == 1)
                    {
                        piece = "bpa";
                    }
                    else if (r == 0)
                    {
                        if (c == 0 || c == BoardSize - 1)
                            piece = "bro";
                        else if (c == 1 || c == BoardSize - 2)
                            piece = "bkn";
                        else if (c == 2 || c == BoardSize - 3)
                            piece = "bbi";
                        else if (c == 3)
                            piece = "bqu";
                        else if (c == 4)
                            piece = "bki";
                        else
                            piece = null;
                    }
                    else if (r == 6)
                        piece = "wpa";
                    else if (r == 7)
                    {
                        if (c == 0 || c == BoardSize - 1)
                            piece = "wro";
                        else if (c == 1 || c == BoardSize - 2)
                            piece = "wkn";
                        else if (c == 2 || c == BoardSize - 3)
                            piece = "wbi";
                        else if (c == 3)
                            piece = "wqu";
                        else if (c == 4)
                            piece = "wki";
                        else
                            piece = null;
                    }
                    else
                        piece = null;


                    sq[i] = new SquareViewModel(i, (r + c) % 2 == 0, piece);
                    i++;
                }
            }
            _squares = new ObservableCollection<SquareViewModel>(sq);
        }

        [ObservableProperty]
        private ObservableCollection<SquareViewModel> _squares;

        // rows
        public string[] Ranks { get; }

        // columns
        public string[] Files { get; }




        public void MovePiece(byte fromIndex, byte toIndex)
        {
            var from = Squares[fromIndex];
            var to = Squares[toIndex];

            to.Piece = from.Piece;
            from.Piece = null;
            from.IsGhost = false;
        }
    }
}