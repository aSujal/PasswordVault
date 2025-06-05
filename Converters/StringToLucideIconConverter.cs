using Avalonia;
using Avalonia.Data.Converters;
using LucideAvalonia;
using LucideAvalonia.Enum;
using System;
using System.Globalization;

namespace PasswordVault.Converters;

public class StringToLucideIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string iconName && !string.IsNullOrEmpty(iconName))
        {
            if (Enum.TryParse<LucideIconNames>(iconName, true, out var lucideIcon))
            {
                return lucideIcon;
            }
            else
            {
                Console.WriteLine($"[StringToLucideIconConverter] Warning: Could not parse icon name '{iconName}' to a LucideIcons enum member.");
                return LucideIconNames.BadgeHelp; // Example fallback
            }
        }
        return AvaloniaProperty.UnsetValue; // or a default icon
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException("ConvertBack is not implemented for StringToLucideIconConverter.");
    }
}
