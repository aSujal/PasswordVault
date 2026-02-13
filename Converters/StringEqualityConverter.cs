using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace PasswordVault.Converters;

public class StringEqualityConverter : IMultiValueConverter
{
    public static readonly StringEqualityConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2)
            return false;

        var val1 = values[0];
        var val2 = values[1];

        var str1 = val1?.ToString();
        var str2 = val2?.ToString();

        return string.Equals(str1, str2, StringComparison.Ordinal);
    }
}
