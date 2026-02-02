using System;
using System.Threading.Tasks;

namespace PasswordVault.Services.Auth;

public interface IAuthService
{
    event EventHandler? Authenticated;
    event EventHandler? Locked;

    bool IsAuthenticated { get; }
    string? CurrentUsername { get; }
    string? CurrentKeyBase64 { get; }

    Task<bool> ValidateMasterPasswordAsync(string masterPassword);
    Task ChangeMasterPasswordAsync(string currentPassword, string newPassword);
    Task<bool> IsAuthenticatedAsync();
    Task LockAsync();
    void NotifyAuthenticated();
    Task<bool> EnableBiometricAuthenticationAsync();
    Task<bool> AuthenticateWithBiometricsAsync();
}
