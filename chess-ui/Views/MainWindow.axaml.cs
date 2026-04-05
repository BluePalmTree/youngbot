using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives; // AdornerLayer
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Svg.Skia;
using chess_ui.ViewModels;

namespace chess_ui.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        #region Drag & Drop Piece
        // ── drag state ────────────────────────────────────────────────
        private SquareViewModel? _dragSource;
        private Image? _ghost;          // the floating image
        private Canvas? _adornerCanvas;  // lives in AdornerLayer
        private Point _grabOffset;     // cursor offset within the piece


        // ── 1. Initiate drag ─────────────────────────────────────────
        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not Border border) return;
            if (border.DataContext is not SquareViewModel sq) return;
            if (sq.Piece is null) return;           // empty square — nothing to drag

            _dragSource = sq;
            sq.IsGhost = true;                     // hide piece at source

            // Build ghost image            
            var svgSrc = SvgSource.Load($"avares://chess-ui/Assets/pieces/{sq.Piece}.svg", baseUri: null);
            var svgImage = new SvgImage { Source = svgSrc };

            var cull = svgSrc.Picture!.CullRect;
            var src = new Rect(cull.Left, cull.Top, cull.Width, cull.Height);

            var bitmap = new RenderTargetBitmap(new PixelSize(64, 64), new Vector(96, 96));
            using (var ctx = bitmap.CreateDrawingContext())
            {
                ((Avalonia.Media.IImage)svgImage).Draw(ctx, src, new Rect(0, 0, 64, 64));
            }

            _ghost = new Image
            {
                Source = bitmap,
                Opacity = 0.65,
                Width = 64,
                Height = 64,
                IsHitTestVisible = false,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };

            // Place ghost in AdornerLayer (floats above everything)
            var boardControl = this.FindControl<ItemsControl>("BoardItems");
            var layer = AdornerLayer.GetAdornerLayer(boardControl!);
            if (layer is not null)
            {
                _adornerCanvas = new Canvas { IsHitTestVisible = false };
                _adornerCanvas.Children.Add(_ghost);
                AdornerLayer.SetAdornedElement(_adornerCanvas, this);
                layer.Children.Add(_adornerCanvas);
            }

            // Centre ghost on cursor
            var pos = e.GetPosition(this);
            _grabOffset = new Point(32, 32);
            MoveGhost(pos);

            e.Pointer.Capture(this); // receive Move/Released even outside window
        }

        // ── 2. Move ghost ─────────────────────────────────────────────
        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_ghost is null) return;
            MoveGhost(e.GetPosition(this));
        }

        // ── 3. Drop ───────────────────────────────────────────────────
        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_dragSource is null || _ghost is null) return;

            RemoveGhost();

            var dropPos = e.GetPosition(this);
            var target = HitTestSquare(dropPos);

            if (target is not null && target != _dragSource)
            {
                var vm = (BoardViewModel)DataContext!;
                vm.MovePiece(_dragSource.Index, target.Index);
            }
            else
            {
                _dragSource.IsGhost = false;        // snap back — illegal or same square
            }

            _dragSource = null;
            //e.Pointer.Release();
        }

        // ── Helpers ───────────────────────────────────────────────────
        private void MoveGhost(Point windowPos)
        {
            if (_ghost is null || _adornerCanvas is null) return;

            Canvas.SetLeft(_ghost, windowPos.X - _grabOffset.X);
            Canvas.SetTop(_ghost, windowPos.Y - _grabOffset.Y);
        }

        private void RemoveGhost()
        {
            if (_adornerCanvas is null) return;

            var boardControl = this.FindControl<ItemsControl>("BoardItems");
            var layer = AdornerLayer.GetAdornerLayer(boardControl!);
            layer?.Children.Remove(_adornerCanvas);

            _adornerCanvas = null;
            _ghost = null;
        }

        /// <summary>
        /// Walk the visual tree under <paramref name="windowPoint"/> to find
        /// which SquareViewModel's Border the cursor is over.
        /// </summary>
        private SquareViewModel? HitTestSquare(Point windowPoint)
        {
            Debug.WriteLine($"Window-Point: {windowPoint}");
            // The board ItemsControl is in Grid col 1, row 1.
            // Each generated container is a ContentPresenter wrapping a Border.
            // We translate the window point into the ItemsControl's coordinate space
            // and check each square's bounding box.

            var boardControl = this.FindControl<ItemsControl>("BoardItems");
            if (boardControl is null) return null;

            foreach (var container in boardControl.GetRealizedContainers())
            {
                if (container is not ContentPresenter cp) continue;
                if (cp.DataContext is not SquareViewModel sq) continue;

                var bounds = cp.Bounds; // relative to ItemsControl
                var topLeft = boardControl.TranslatePoint(bounds.TopLeft, this);
                if (topLeft is null) continue;

                Debug.WriteLine($"Square {sq.Index:00} | Top-Left: x:{topLeft.Value.X:000} y:{topLeft.Value.Y:000}");

                var rect = new Rect(topLeft.Value, bounds.Size);
                if (rect.Contains(windowPoint))
                    return sq;
            }
            return null;
        }
        #endregion        
    }
}