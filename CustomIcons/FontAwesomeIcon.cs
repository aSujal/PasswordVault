using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace PasswordVault.CustomIcons;

/// <summary>
/// A custom Avalonia control that displays Font Awesome 7 icons.
/// Inherits from TextBlock to automatically inherit styling and layout properties.
/// </summary>
public class FontAwesomeIcon : TextBlock
{
    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<FontAwesomeIcon, string?>(nameof(Value));

    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    static FontAwesomeIcon()
    {
        ValueProperty.Changed.AddClassHandler<FontAwesomeIcon>((x, e) => x.OnValueChanged(e));
        FontSizeProperty.OverrideMetadata(typeof(FontAwesomeIcon), new StyledPropertyMetadata<double>(16.0));
    }

    public FontAwesomeIcon()
    {
        // Center the icon by default to match typical icon usage
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        TextAlignment = TextAlignment.Center;
    }

    private void OnValueChanged(AvaloniaPropertyChangedEventArgs e)
    {
        UpdateIcon();
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        UpdateIcon();
    }

    private void UpdateIcon()
    {
        var val = Value;
        if (string.IsNullOrWhiteSpace(val))
        {
            Text = string.Empty;
            return;
        }

        var (style, iconKey) = ParseIconValue(val);

        if (FontAwesomeGlyphMap.TryGetGlyph(iconKey, out var glyph))
        {
            Text = glyph.ToString();

            // Try to look up the font family from application resources
            string resourceKey = style switch
            {
                FontAwesomeGlyphMap.Brands => "FontAwesomeBrands",
                FontAwesomeGlyphMap.Regular => "FontAwesomeRegular",
                _ => "FontAwesomeSolid"
            };

            if (Application.Current != null && Application.Current.TryFindResource(resourceKey, out var resourceValue) && resourceValue is FontFamily fontFamily)
            {
                FontFamily = fontFamily;
            }
            else
            {
                // Fallback to programmatically loading the font family if not found in application resources
                FontFamily = style switch
                {
                    FontAwesomeGlyphMap.Brands => new FontFamily("avares://PasswordVault/Assets/Fonts/Font Awesome 7 Brands-Regular-400.otf#Font Awesome 7 Brands"),
                    FontAwesomeGlyphMap.Regular => new FontFamily("avares://PasswordVault/Assets/Fonts/Font Awesome 7 Free-Regular-400.otf#Font Awesome 7 Free Regular"),
                    _ => new FontFamily("avares://PasswordVault/Assets/Fonts/Font Awesome 7 Free-Solid-900.otf#Font Awesome 7 Free Solid")
                };
            }
        }
        else
        {
            Text = string.Empty;
        }
    }

    /// <summary>
    /// Parses the style prefix and the icon name from a string like "fa-solid fa-key".
    /// </summary>
    public static (string Style, string IconKey) ParseIconValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return (FontAwesomeGlyphMap.Solid, string.Empty);

        var parts = value.Split(new[] { ' ', ':', '/' }, StringSplitOptions.RemoveEmptyEntries);
        string style = FontAwesomeGlyphMap.Solid;
        string name = string.Empty;

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.Equals("fa-solid", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("solid", StringComparison.OrdinalIgnoreCase))
            {
                style = FontAwesomeGlyphMap.Solid;
            }
            else if (trimmed.Equals("fa-regular", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("regular", StringComparison.OrdinalIgnoreCase))
            {
                style = FontAwesomeGlyphMap.Regular;
            }
            else if (trimmed.Equals("fa-brands", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("brands", StringComparison.OrdinalIgnoreCase))
            {
                style = FontAwesomeGlyphMap.Brands;
            }
            else
            {
                name = trimmed;
            }
        }

        if (string.IsNullOrEmpty(name) && parts.Length > 0)
        {
            name = parts[^1];
        }

        if (!name.StartsWith("fa-", StringComparison.OrdinalIgnoreCase))
        {
            name = "fa-" + name;
        }

        return (style, name);
    }
}
