using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;

namespace chess_ui.Converters
{
    public class IsInCollectionConverter : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count != 2)
                return false;

            if (values[0] is int i)
            {
                if (values[1] is IEnumerable<int> ints)
                {
                    return ints.Contains(i);
                }
            }

            return false;
        }
    }
}