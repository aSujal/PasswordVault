using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace PasswordVault.Converters;

// Converts a DocumentFolderNode.Depth (0 = root) into a left margin so the folders sidebar can
// show nesting without a recursive TreeView template.
internal class DepthIndentConverter : IValueConverter
{
    public static readonly DepthIndentConverter Instance = new();

    private const double IndentPerLevel = 16;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var depth = value is int i ? i : 0;
        return new Thickness(depth * IndentPerLevel, 0, 0, 0);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
