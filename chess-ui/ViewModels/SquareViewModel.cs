using CommunityToolkit.Mvvm.ComponentModel;

namespace chess_ui.ViewModels
{
    public partial class SquareViewModel : ViewModelBase
    {
        public SquareViewModel(byte index, bool isLight, string? piece = null)
        {
            Index = index;
            IsLight = isLight;
            Piece = piece;
        }


        public byte Index { get; }
        public bool IsLight { get; }

        [ObservableProperty]
        private string? _piece;

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private bool _isGhost;
    }
}