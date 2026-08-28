using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PasswordVault.Models;
using PasswordVault.Services.Crypto;
using PasswordVault.Services.Database;
using Xunit;

namespace PasswordVault.Tests.Services;

// Covers the master-password change path end to end against a real (temp) LiteDB file,
// because a previous regression here silently left the vault encrypted under the OLD
// password while the app believed the new one was in effect -- a bug that only a real
// rebuild + reopen can catch, not a mocked one.
public class DatabaseServicePasswordChangeTests : IDisposable
{
    private readonly string _tempFolder;
    private readonly CryptoService _cryptoService;

    public DatabaseServicePasswordChangeTests()
    {
        _tempFolder = Path.Combine(Path.GetTempPath(), "PasswordVaultTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempFolder);
        _cryptoService = new CryptoService(new byte[32]);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempFolder, recursive: true); } catch { /* best effort cleanup */ }
    }

    [Fact]
    public async Task ChangeDatabasePasswordAsync_ReEncryptsVault_OldPasswordNoLongerOpensIt()
    {
        var db = new DatabaseService(_cryptoService, _tempFolder);

        await db.InitializeDatabaseAsync("old-master-password");
        // InitializeDatabaseAsync derives its own salt internally, so the real key is
        // whatever it persisted on the user record -- read it back rather than re-deriving.
        string oldHash = (await db.GetUserAsync())!.PasswordHash;

        // Seed a real entry so we can prove data survives the rebuild, not just that it opens.
        db.OpenDatabase().GetCollection<Password>("passwords").Insert(new Password { Title = "Test Entry" });

        byte[] newSalt = _cryptoService.GenerateRandomBytes(16);
        string newHash = _cryptoService.DeriveKeyFromPassword("new-master-password", newSalt);

        await db.ChangeDatabasePasswordAsync(newHash);
        db.LockDatabase();

        await db.SetEncryptionKeyAsync(Convert.FromBase64String(oldHash));
        Assert.ThrowsAny<Exception>(() => db.OpenDatabase().GetCollection<Password>("passwords").Count());

        db.LockDatabase();
        await db.SetEncryptionKeyAsync(Convert.FromBase64String(newHash));
        var reopened = db.OpenDatabase().GetCollection<Password>("passwords");
        Assert.Equal(1, reopened.Count());
        Assert.Equal("Test Entry", reopened.FindAll().First().Title);
    }

    // AuthService.ChangeMasterPasswordAsync prepares a rekeyed vault and a new user.dat as
    // temp files, then commits both back to back. This proves the "prepare" half alone -
    // simulating the process dying before either commit runs - leaves the original vault.db
    // and user.dat completely untouched and still openable with the OLD password, instead of
    // silently splitting into a half-changed state.
    [Fact]
    public async Task PrepareVaultRekeyAndUserData_WithoutCommitting_LeavesOriginalFilesIntact()
    {
        var db = new DatabaseService(_cryptoService, _tempFolder);

        await db.InitializeDatabaseAsync("old-master-password");
        var user = (await db.GetUserAsync())!;
        string oldHash = user.PasswordHash;

        db.OpenDatabase().GetCollection<Password>("passwords").Insert(new Password { Title = "Test Entry" });

        byte[] newSalt = _cryptoService.GenerateRandomBytes(16);
        string newHash = _cryptoService.DeriveKeyFromPassword("new-master-password", newSalt);

        // "Prepare" both artifacts, then stop - as if the process crashed right here.
        _ = await db.PrepareVaultRekeyAsync(newHash);
        user.PasswordHash = newHash;
        user.PasswordSalt = newSalt;
        _ = await db.PrepareUserDataAsync(user);

        db.LockDatabase();
        await db.SetEncryptionKeyAsync(Convert.FromBase64String(oldHash));

        var stillOpenable = db.OpenDatabase().GetCollection<Password>("passwords");
        Assert.Equal(1, stillOpenable.Count());
        Assert.Equal("Test Entry", stillOpenable.FindAll().First().Title);

        db.LockDatabase();
        var reloadedUser = (await new DatabaseService(_cryptoService, _tempFolder).GetUserAsync())!;
        Assert.Equal(oldHash, reloadedUser.PasswordHash);
    }
}
