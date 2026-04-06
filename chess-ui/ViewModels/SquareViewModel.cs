using CommunityToolkit.Mvvm.ComponentModel;

namespace chess_ui.ViewModels
{
    public partial class SquareViewModel : ViewModelBase
    {
        public int UiIndex { get; }
        public bool IsLight { get; }

        public SquareViewModel(int uiIndex, int boardIndex, bool isLight)
        {
            UiIndex = uiIndex;
            IsLight = isLight;
            _boardIndex = boardIndex;
        }

        [ObservableProperty]
        private int _boardIndex;

        [ObservableProperty]
        private string? _piece;

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private bool _isGhost;

        [ObservableProperty]
        private bool _isHighlighted;
    }
}