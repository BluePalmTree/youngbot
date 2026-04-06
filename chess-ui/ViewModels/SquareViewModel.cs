using CommunityToolkit.Mvvm.ComponentModel;

namespace chess_ui.ViewModels
{
    public partial class SquareViewModel : ViewModelBase
    {
        public int UiIndex { get; }
        public bool IsLight { get; }

        public SquareViewModel(int uiIndex, bool isLight)
        {
            UiIndex = uiIndex;
            IsLight = isLight;
            _isSelected = false;
            _isHighlighted = false;
        }

        [ObservableProperty]
        private int _engineIndex;

        [ObservableProperty]
        private string? _piece;

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private bool _isGhost;

        [ObservableProperty]
        private bool _isHighlighted;

        [ObservableProperty]
        private bool _isValidMoveTarget;
    }
}