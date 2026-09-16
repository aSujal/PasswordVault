using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace PasswordVault.Converters;

// Maps a VaultDocument.FileName's extension to a FontAwesomeIcon Value string for the document
// list and preview dialog. Every glyph name returned here must exist in FontAwesomeGlyphMap -
// an unmapped name silently renders as blank space.
internal class DocumentIconConverter : IValueConverter
{
    public static readonly DocumentIconConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var fileName = value as string ?? string.Empty;
        var extension = System.IO.Path.GetExtension(fileName).ToLowerInvariant();

        return extension switch
        {
            ".pdf" => "fa-solid fa-file-pdf",
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".bmp" => "fa-solid fa-image",
            ".csv" => "fa-solid fa-file-csv",
            ".json" or ".xml" => "fa-solid fa-file-code",
            _ => "fa-solid fa-file-lines",
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
