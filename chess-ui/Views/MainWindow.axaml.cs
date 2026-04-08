using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Svg.Skia;
using chess_engine.Models;
using chess_ui.ViewModels;

namespace chess_ui.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        public void BoardItems_DataContextChanged(object? sender, EventArgs e)
        {
            base.OnDataContextChanged(e);

            if (DataContext is BoardViewModel vm)
                vm.PromotionRequired += ShowPromotionFlyout;
        }

        private void ShowPromotionFlyout(int targetUiIndex)
        {
            if (BoardItems.ContainerFromIndex(targetUiIndex) is not Control container) return;

            var vm = (BoardViewModel?)DataContext;
            if (vm is null) return;

            var flyout = new MenuFlyout
            {
                Placement = PlacementMode.BottomEdgeAlignedLeft
            };
            var items = new[]
            {
                ("Queen", MoveFlag.PromoteQueen),
                ("Rook", MoveFlag.PromoteRook),
                ("Bishop", MoveFlag.PromoteBishop),
                ("Knight", MoveFlag.PromoteKnight),
            };

            foreach (var (label, flag) in items)
            {
                var item = new MenuItem { Header = label };
                item.Click += (_, _) =>
                {
                    flyout.Hide();
                    vm.CompletePromotion(flag);
                };
                flyout.Items.Add(item);
            }

            flyout.ShowAt(container);
        }

        #region Drag & Drop Piece
        private const double DragThreshold = 4.0;

        // ── drag state ────────────────────────────────────────────────
        private SquareViewModel? _dragSource;
        private Image? _ghost; // the floating image
        private Canvas? _adornerCanvas; // lives in AdornerLayer
        private Point _grabOffset; // cursor offset within the piece
        private Point _pressPosition; // where the mouse went down
        private bool _dragging; // true once threshol is crossed

        // ── 1. Initiate drag ─────────────────────────────────────────
        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not Border border) return;
            if (border.DataContext is not SquareViewModel sq) return;
            if (string.IsNullOrWhiteSpace(sq.Piece)) return;

            _dragSource = sq;

            // Clear old selection highlight, set new one
            var vm = (BoardViewModel)DataContext!;
            foreach (var s in vm.Squares)
                s.IsSelected = false;

            //sq.IsSelected = true;
            vm.SelectSquare(sq.UiIndex);

            _pressPosition = e.GetPosition(this);
            _dragging = false;

            e.Pointer.Capture(this);
        }

        // ── 2. Move ghost ─────────────────────────────────────────────
        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_dragSource is null) return;

            var pos = e.GetPosition(this);

            if (!_dragging)
            {
                var dx = pos.X - _pressPosition.X;
                var dy = pos.Y - _pressPosition.Y;
                if (dx * dx + dy * dy < DragThreshold * DragThreshold) return;

                // Threshold crossed — start the drag
                _dragging = true;
                _dragSource.IsGhost = true;

                var svgSrc = SvgSource.Load($"avares://chess-ui/Assets/pieces/{_dragSource.Piece}.svg", baseUri: null);
                var svgImage = new SvgImage { Source = svgSrc };
                var cull = svgSrc.Picture!.CullRect;
                var src = new Rect(cull.Left, cull.Top, cull.Width, cull.Height);

                var bitmap = new RenderTargetBitmap(new PixelSize(64, 64), new Vector(96, 96));
                using (var ctx = bitmap.CreateDrawingContext())
                    ((Avalonia.Media.IImage)svgImage).Draw(ctx, src, new Rect(0, 0, 64, 64));

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

                var boardControl = this.FindControl<ItemsControl>("BoardItems");
                var layer = AdornerLayer.GetAdornerLayer(boardControl!);
                if (layer is not null)
                {
                    _adornerCanvas = new Canvas { IsHitTestVisible = false };
                    _adornerCanvas.Children.Add(_ghost);
                    AdornerLayer.SetAdornedElement(_adornerCanvas, this);
                    layer.Children.Add(_adornerCanvas);
                }

                _grabOffset = new Point(32, 32);
            }

            MoveGhost(pos);
        }

        // ── 3. Drop ───────────────────────────────────────────────────
        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_dragSource is null) return;

            if (_dragging)
            {
                RemoveGhost();

                var dropPos = e.GetPosition(this);
                var target = HitTestSquare(dropPos);

                if (target is not null && target != _dragSource)
                {
                    var vm = (BoardViewModel)DataContext!;
                    if (!vm.TryMovePiece(_dragSource.UiIndex, target.UiIndex))
                        _dragSource.IsGhost = false;
                }
                else
                {
                    _dragSource.IsGhost = false;
                }
            }

            _dragSource = null;
            _dragging = false;
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

                //Debug.WriteLine($"Square {sq.UiIndex:00} | Top-Left: x:{topLeft.Value.X:000} y:{topLeft.Value.Y:000}");

                var rect = new Rect(topLeft.Value, bounds.Size);
                if (rect.Contains(windowPoint))
                    return sq;
            }
            return null;
        }
        #endregion        
    }
}