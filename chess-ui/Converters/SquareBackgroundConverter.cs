using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace chess_ui.Converters
{
    public class SquareBackgroundConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is true ? "#ffce9e" : "#d18b47";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}