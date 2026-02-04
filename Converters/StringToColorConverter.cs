using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace PasswordVault.Converters;

public class StringToColorConverter : IValueConverter
{
    public static StringToColorConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrEmpty(hex))
        {
            if (Color.TryParse(hex, out var color))
            {
                return color;
            }
        }
        return Colors.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Color color)
        {
            return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
        }
        return null;
    }
}
