using System;
using System.Globalization;
using Avalonia.Data.Converters;
using chess_engine.Enums;

namespace chess_ui.Converters
{
    public class IsNotPlayingConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is GameStatus status && status != GameStatus.Playing;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}