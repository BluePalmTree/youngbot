using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace chess_ui.Converters
{
    public class CastlingRightsConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            string text = "";

            if (value is int castlingRights && castlingRights > 0)
            {
                text += (castlingRights & 0b1000) != 0 ? "K" : "";
                text += (castlingRights & 0b0100) != 0 ? "Q" : "";
                text += (castlingRights & 0b0010) != 0 ? "k" : "";
                text += (castlingRights & 0b0001) != 0 ? "q" : "";
            }
            else
                text = "-";

            return text;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}