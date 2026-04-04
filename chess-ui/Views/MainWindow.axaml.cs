using System.Data;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media;

namespace chess_ui.Views
{
    public partial class MainWindow : Window
    {
        // The grid is 8 real squares + 2 border cells on each axis
        private const int BoardSize = 8;
        private const int GridSize = BoardSize + 2;
        private const int CellSize = 80;

        public MainWindow()
        {
            InitializeComponent();
            Content = BuildBoardGrid();
        }

        private static Grid BuildBoardGrid()
        {
            var cellSizes = string.Join(",", Enumerable.Repeat(CellSize, GridSize));

            var board = new Grid
            {
                ShowGridLines = false,
                ColumnDefinitions = ColumnDefinitions.Parse(cellSizes),
                RowDefinitions = RowDefinitions.Parse(cellSizes)
            };

            for (int r = 0; r < GridSize; r++)
            {
                for (int c = 0; c < GridSize; c++)
                {
                    bool isInnerCell = r > 0 && r < GridSize - 1 && c > 0 && c < GridSize - 1;

                    if (isInnerCell)
                        board.Children.Add(CreateSquareBackground(r, c));

                    board.Children.Add(CreateCellLabel(r, c, isInnerCell));
                }
            }

            return board;
        }

        private static Border CreateSquareBackground(int r, int c)
        {
            // Squares are light when row and column parity match
            bool isLightSquare = (r + c) % 2 == 0;

            var background = new Border
            {
                Background = Brush.Parse(isLightSquare ? "#ffce9e" : "#d18b47"),
            };

            Grid.SetRow(background, r);
            Grid.SetColumn(background, c);

            return background;
        }

        private static TextBlock CreateCellLabel(int r, int c, bool isInnerCell)
        {
            var label = new TextBlock
            {
                Text = GetCellLabelText(r, c),
                Foreground = isInnerCell ? Brushes.Gray : Brushes.Black,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            };

            Grid.SetRow(label, r);
            Grid.SetColumn(label, c);

            return label;
        }

        private static string GetCellLabelText(int r, int c)
        {
            bool isTopOrBottomBorder = r == 0 || r == GridSize - 1;
            bool isLeftOrRightBorder = c == 0 || c == GridSize - 1;

            // Corner cells are blank
            if (isTopOrBottomBorder && isLeftOrRightBorder)
                return string.Empty;

            // Top/bottom border rows show file letters (a–h), offset by 1 for the border column
            if (isTopOrBottomBorder)
                return ((char)('a' + c - 1)).ToString();

            // Left/right border columns show rank numbers (1–8), counting up from the bottom
            if (isLeftOrRightBorder)
                return (GridSize - 1 - r).ToString();

            // Inner cells — placeholder until piece rendering is implemented
            return string.Empty;
        }
    }
}