using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace chess_ui.Converters
{
    public class NumbersEqualConverterMulti : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count < 2)
                return false;

            decimal? first = null;

            foreach (var value in values)
            {
                if (!TryToDecimal(value, culture, out var current))
                    return false;

                if (first is null)
                    first = current;
                else if (current != first.Value)
                    return false;
            }

            return true;
        }

        // Accept only primitive numeric types. `decimal` is the widest common
        // denominator that compares integers exactly and holds float/double
        // without precision surprises within its range.
        private static bool TryToDecimal(object? value, CultureInfo culture, out decimal result)
        {
            switch (value)
            {
                case byte or sbyte or short or ushort or int or uint or long or ulong
                     or float or double or decimal:
                    result = System.Convert.ToDecimal(value, culture);
                    return true;
                default:
                    result = 0m;
                    return false;
            }
        }
    }
}
