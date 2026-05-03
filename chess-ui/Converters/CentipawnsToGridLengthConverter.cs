using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace chess_ui.Converters
{
    public class CentipawnsToGridLengthConverter : IValueConverter
    {
        private const double k = 400.0;

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            int cp = value is int v ? v : 0;
            double whiteFill = 0.5 + 0.5 * Math.Tanh(cp / k); // 0..1
            double fraction = (parameter as string) == "white" ? whiteFill : 1.0 - whiteFill;
            return new GridLength(fraction, GridUnitType.Star);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}