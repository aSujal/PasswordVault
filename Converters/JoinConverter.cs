using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Data.Converters;

namespace PasswordVault.Converters;

internal class JoinConverter : IValueConverter
{
    public string Separator { get; set; } = ", ";

    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is IEnumerable<string> strings && parameter is string separator)
        {
            return string.Join(separator, strings);
        }
        return string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
