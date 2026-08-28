using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using PasswordVault.Models;
using PasswordVault.Services.Crypto;

namespace PasswordVault.Services.AI;

public class AiSettingsService
{
    private readonly string _settingsFile;
    private readonly ICryptoService _cryptoService;
    private AiSettings? _cached;

    public AiSettingsService(ICryptoService cryptoService)
    {
        _cryptoService = cryptoService;
        var appFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PasswordManager");
        if (!Directory.Exists(appFolder)) Directory.CreateDirectory(appFolder);
        _settingsFile = Path.Combine(appFolder, "ai_settings.dat");
    }

    public event EventHandler? SettingsChanged;

    public async Task<AiSettings> LoadAsync()
    {
        if (_cached != null) return _cached;
        if (!File.Exists(_settingsFile))
        {
            _cached = new AiSettings();
            return _cached;
        }

        try
        {
            var encryptedData = await File.ReadAllBytesAsync(_settingsFile);
            var json = _cryptoService.Decrypt(encryptedData);
            _cached = JsonSerializer.Deserialize<AiSettings>(json) ?? new AiSettings();
            return _cached;
        }
        catch
        {
            // If decryption fails (e.g. key changed), reset to defaults
            _cached = new AiSettings();
            return _cached;
        }
    }

    /// <summary>
    /// Persist settings to disk (encrypted).
    /// </summary>
    public async Task SaveAsync(AiSettings settings)
    {
        _cached = settings;
        var json = JsonSerializer.Serialize(settings);
        var encrypted = _cryptoService.Encrypt(json);
        await File.WriteAllBytesAsync(_settingsFile, encrypted);
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Get the current cached settings without disk I/O.
    /// Returns null if LoadAsync has never been called.
    /// </summary>
    public AiSettings? GetCurrent() => _cached;
}
