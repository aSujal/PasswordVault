using System;
using System.IO;
using Avalonia;
using Avalonia.Styling;

namespace PasswordVault.Services;

public enum AppTheme { System, Light, Dark, Glass }

// Applies the app theme and remembers it in a plain file next to the vault. It is read before
// login (the lock screen is themed too), so it can't live in the encrypted user record.
public class ThemeService
{
    // Falls back to Dark for any resource Themes/Glass.axaml doesn't override.
    public static readonly ThemeVariant Glass = new("Glass", ThemeVariant.Dark);

    private readonly string _settingsFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PasswordManager", "theme.txt");

    public void ApplySaved()
    {
        string? saved = null;
        try { saved = File.ReadAllText(_settingsFile).Trim(); } catch { /* first run */ }
        Apply(Enum.TryParse(saved, out AppTheme theme) ? theme : AppTheme.Glass);
    }

    public void SetTheme(AppTheme theme)
    {
        Apply(theme);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsFile)!);
            File.WriteAllText(_settingsFile, theme.ToString());
        }
        catch { /* the theme still applies for this session */ }
    }

    private static void Apply(AppTheme theme) =>
        Application.Current!.RequestedThemeVariant = theme switch
        {
            AppTheme.Light => ThemeVariant.Light,
            AppTheme.Dark => ThemeVariant.Dark,
            AppTheme.Glass => Glass,
            _ => ThemeVariant.Default
        };
}
