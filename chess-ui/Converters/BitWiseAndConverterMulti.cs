using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace chess_ui.Converters
{
    public class BitWiseAndConvertMulti : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count != 2)
                return false;

            if (values[0] is int value1)
            {
                if (values[1] is ulong value2)
                {
                    return (value2 & 1UL << value1) != 0;
                }
            }

            return false;
        }
    }
}