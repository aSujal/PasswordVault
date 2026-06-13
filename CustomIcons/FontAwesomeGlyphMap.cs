using System;
using System.Collections.Generic;

namespace PasswordVault.CustomIcons;

/// <summary>
/// Maps Font Awesome icon names (e.g. "fa-key") to their Unicode codepoints.
/// Covers all icons used in this project for both Solid and Regular styles,
/// plus icons used in the AddCategoryDialog icon picker.
/// </summary>
public static class FontAwesomeGlyphMap
{
    // Style prefixes
    public const string Solid = "fa-solid";
    public const string Regular = "fa-regular";
    public const string Brands = "fa-brands";

    /// <summary>
    /// Master map: icon short-name → Unicode codepoint.
    /// FA7 retains the same codepoints for the vast majority of FA6 icons.
    /// </summary>
    public static readonly Dictionary<string, char> Glyphs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["fa-columns"] = '\uf0db',
        ["fa-tachometer-alt"] = '\uf3fd',
        ["fa-key"] = '\uf084',
        ["fa-sync"] = '\uf021',
        ["fa-cog"] = '\uf013',
        ["fa-check"] = '\uf00c',
        ["fa-check-square"] = '\uf14a',
        ["fa-plus"] = '\uf067',
        ["fa-trash"] = '\uf1f8',
        ["fa-pen"] = '\uf304',
        ["fa-times"] = '\uf00d',
        ["fa-ellipsis-v"] = '\uf142',
        ["fa-search"] = '\uf002',
        ["fa-sliders"] = '\uf1de',
        ["fa-arrows-rotate"] = '\uf021',
        ["fa-rotate"] = '\uf2f1',

        ["fa-sun"] = '\uf185',
        ["fa-moon"] = '\uf186',
        ["fa-desktop"] = '\uf108',
        ["fa-tags"] = '\uf02c',
        ["fa-download"] = '\uf019',
        ["fa-circle-check"] = '\uf058',

        ["fa-clipboard"] = '\uf328',
        ["fa-star"] = '\uf005',
        ["fa-triangle-exclamation"] = '\uf071',
        ["fa-file-pen"] = '\uf31c',
        ["fa-copy"] = '\uf0c5',

        ["fa-file-csv"] = '\uf6dd',
        ["fa-file-code"] = '\uf1c9',
        ["fa-upload"] = '\uf093',

        ["fa-globe"] = '\uf0ac',
        ["fa-user"] = '\uf007',
        ["fa-lock"] = '\uf023',
        ["fa-wand-magic-sparkles"] = '\ue2ca',
        ["fa-link"] = '\uf0c1',
        ["fa-file-lines"] = '\uf15c',

        ["fa-folder-open"] = '\uf07c',

        ["fa-envelope"] = '\uf0e0',
        ["fa-credit-card"] = '\uf09d',
        ["fa-building"] = '\uf1ad',
        ["fa-shopping-cart"] = '\uf07a',
        ["fa-heart"] = '\uf004',
        ["fa-shield"] = '\uf132',
        ["fa-wifi"] = '\uf1eb',
        ["fa-laptop"] = '\uf109',
        ["fa-cloud"] = '\uf0c2',
        ["fa-mobile"] = '\uf3ce',
        ["fa-code"] = '\uf121',
        ["fa-database"] = '\uf1c0',
        ["fa-gamepad"] = '\uf11b',
        ["fa-book"] = '\uf02d',
        ["fa-camera"] = '\uf030',
        ["fa-user-shield"] = '\uf505',
        ["fa-id-card"] = '\uf2c2',
        ["fa-passport"] = '\uf5ab',
        ["fa-sim-card"] = '\uf7c4',
        ["fa-server"] = '\uf233',
        ["fa-user-secret"] = '\uf21b',
        ["fa-file-invoice"] = '\uf570',
        ["fa-clipboard-list"] = '\uf46d',
        ["fa-building-columns"] = '\uf19c',
        ["fa-vault"] = '\ue2c5',
        ["fa-tag"] = '\uf02b',
        ["fa-briefcase"] = '\uf0b1',

        ["fa-instagram"] = '\uf16d',
    };

    /// <summary>
    /// Attempt to look up the glyph character for the given short icon name.
    /// </summary>
    public static bool TryGetGlyph(string iconName, out char glyph)
    {
        return Glyphs.TryGetValue(iconName, out glyph);
    }
}
