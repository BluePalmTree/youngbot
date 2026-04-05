using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Svg.Skia;

namespace chess_ui.Converters
{
    public class PieceToImageConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string piece)
            {
                var source = SvgSource.Load($"avares://chess-ui/Assets/pieces/{piece}.svg", baseUri: null);

                return new SvgImage { Source = source };
            }

            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}