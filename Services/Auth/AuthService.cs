using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LiteDB;
using PasswordVault.Models;
using PasswordVault.Services.Crypto;
using PasswordVault.Services.Database;

namespace PasswordVault.Services.Auth;

public partial class AuthService(ICryptoService cryptoService, DatabaseService databaseService) : IAuthService
{
    private readonly ICryptoService _cryptoService = cryptoService ?? throw new ArgumentNullException(nameof(cryptoService));
    private readonly DatabaseService _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
    private bool _isAuthenticated = false;
    private string? _currentUsername;
    private string? _currentKeyBase64;

    public event EventHandler? Authenticated;
    public event EventHandler? Locked;

    public bool IsAuthenticated => _isAuthenticated;
    public string? CurrentUsername => _currentUsername;
    public string? CurrentKeyBase64 => _currentKeyBase64;

    public async Task<bool> ValidateMasterPasswordAsync(string masterPassword)
    {
        if (string.IsNullOrEmpty(masterPassword))
            return false;

        var user = await _databaseService.GetUserAsync();
        if (user == null)
            return false;

        string hash = _cryptoService.DeriveKeyFromPassword(masterPassword, user.PasswordSalt);

        bool ok = hash == user.PasswordHash;
        if (ok)
        {
            _isAuthenticated = true;
            _currentUsername = user.Username;
            _currentKeyBase64 = hash;
            byte[] derivedKey = Convert.FromBase64String(user.PasswordHash);
            await _databaseService.SetEncryptionKeyAsync(derivedKey);
            Authenticated?.Invoke(this, EventArgs.Empty);

            _ = _databaseService.AutoBackupAsync();
        }

        return ok;
    }

    public async Task ChangeMasterPasswordAsync(string currentPassword, string newPassword)
    {
        if (string.IsNullOrEmpty(currentPassword))
            throw new UnauthorizedAccessException("Current password is incorrect");

        var user = await _databaseService.GetUserAsync()
                  ?? throw new InvalidOperationException("User record missing.");

        string currentHash = _cryptoService.DeriveKeyFromPassword(currentPassword, user.PasswordSalt);
        if (currentHash != user.PasswordHash)
            throw new UnauthorizedAccessException("Current password is incorrect");

        if (string.IsNullOrEmpty(newPassword))
            throw new ArgumentException("New password cannot be empty");

        // Make sure the database is unlocked with the CURRENT key before we ask it to
        // rebuild under the new one. Deliberately avoids ValidateMasterPasswordAsync here:
        // that method fires the Authenticated event (re-triggering page reloads and an
        // AutoBackup) which can race with the file rebuild below.
        await _databaseService.SetEncryptionKeyAsync(Convert.FromBase64String(currentHash));

        byte[] newSalt = _cryptoService.GenerateRandomBytes(16);
        string newHash = _cryptoService.DeriveKeyFromPassword(newPassword, newSalt);

        // Prepare both the rekeyed vault and the new user record as temp files WITHOUT
        // touching the live vault.db/user.dat. Only once both are ready do we
        // commit them, back to back. If anything throws before both commits run, the live
        // vault and user.dat are untouched and the user can just retry - there's no window
        // where the two files disagree about which password is current.
        string tempVault = await _databaseService.PrepareVaultRekeyAsync(newHash);

        user.PasswordHash = newHash;
        user.PasswordSalt = newSalt;
        string tempUser = await _databaseService.PrepareUserDataAsync(user);

        await _databaseService.CommitVaultRekeyAsync(tempVault, newHash);
        await _databaseService.CommitUserDataAsync(tempUser, user);

        _isAuthenticated = true;
        _currentUsername = user.Username;
        _currentKeyBase64 = newHash;
    }

    public Task<bool> IsAuthenticatedAsync()
    {
        return Task.FromResult(_isAuthenticated);
    }

    public Task LockAsync()
    {
        _isAuthenticated = false;
        _currentUsername = null;
        _currentKeyBase64 = null;
        _databaseService.LockDatabase();
        Locked?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }
    public void NotifyAuthenticated()
    {
        Authenticated?.Invoke(this, EventArgs.Empty);
    }

    public Task<bool> EnableBiometricAuthenticationAsync() => Task.FromResult(false);
    public Task<bool> AuthenticateWithBiometricsAsync() => Task.FromResult(false);
}
