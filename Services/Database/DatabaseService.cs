using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LiteDB;
using PasswordVault.Models;
using PasswordVault.Services.Crypto;

namespace PasswordVault.Services.Database;

public interface IDatabaseService
{
    Task InitializeDatabaseAsync(string masterPassword);
    Task<User?> GetUserAsync();
    Task UpdateUserAsync(User user);
    Task<bool> IsDatabaseInitializedAsync();
    Task BackupDatabaseAsync(string backupPath);
    Task AutoBackupAsync();
    Task ForceBackupAsync();
    Task RestoreDatabaseAsync(string backupPath, string masterPassword);
    Task ChangeDatabasePasswordAsync(string newPasswordBase64);
    Task<string> PrepareVaultRekeyAsync(string newPasswordBase64);
    Task CommitVaultRekeyAsync(string tempFile, string newPasswordBase64);
    Task<string> PrepareUserDataAsync(User user);
    Task CommitUserDataAsync(string tempFile, User user);
}

public class DatabaseService : IDatabaseService
{
    private readonly string _databaseFile;
    private readonly string _userDataFile;
    private readonly ICryptoService _cryptoService;
    private LiteDatabase? _databaseInstance;
    private readonly object _dbLock = new();

    public event EventHandler? DatabaseInitialized;

    private byte[]? _encryptionKey;
    private User? _cachedUser;
    public DatabaseService(ICryptoService cryptoService)
        : this(cryptoService, Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PasswordManager"))
    {
    }

    // Lets tests point the vault at a temp folder instead of the real LocalApplicationData
    // path, so behavior like ChangeDatabasePasswordAsync can be exercised against a throwaway
    // database file rather than a real, potentially populated vault.
    internal DatabaseService(ICryptoService cryptoService, string appFolder)
    {
        _cryptoService = cryptoService ?? throw new ArgumentNullException(nameof(cryptoService));

        _databaseFile = Path.Combine(appFolder, "vault.db");  // LiteDB file
        _userDataFile = Path.Combine(appFolder, "user.dat");  // encrypted user blob

        if (!Directory.Exists(appFolder)) Directory.CreateDirectory(appFolder);
    }

    public LiteDatabase OpenDatabase()
    {
        if (_encryptionKey == null)
            throw new InvalidOperationException("Database not unlocked - call Initialize or supply master password.");
        lock (_dbLock)
        {
            _databaseInstance ??= new LiteDatabase(new ConnectionString
            {
                Filename = _databaseFile,
                Password = Convert.ToBase64String(_encryptionKey)
            });
            return _databaseInstance;
        }
    }

    public async Task SetEncryptionKeyAsync(byte[] encryptionKey)
    {
        ArgumentNullException.ThrowIfNull(encryptionKey);
        lock (_dbLock)
        {
            if (_databaseInstance != null)
            {
                _databaseInstance.Dispose();
                _databaseInstance = null;
            }

            _encryptionKey = encryptionKey.ToArray();
        }
        await Task.CompletedTask;
    }

    public void LockDatabase()
    {
        lock (_dbLock)
        {
            _databaseInstance?.Dispose();
            _databaseInstance = null;
            _encryptionKey = null;
        }
    }

    // Convenience wrapper for callers (and tests) that don't need to interleave the vault
    // swap with another commit (e.g. AuthService.ChangeMasterPasswordAsync, which must also
    // commit a new user.dat and needs both swaps to happen back-to-back - see
    // PrepareVaultRekeyAsync/CommitVaultRekeyAsync).
    public async Task ChangeDatabasePasswordAsync(string newPasswordBase64)
    {
        string tempFile = await PrepareVaultRekeyAsync(newPasswordBase64);
        await CommitVaultRekeyAsync(tempFile, newPasswordBase64);
    }

    // Builds a fully rekeyed copy of the vault under newPasswordBase64 without touching
    // _databaseFile. Safe to call even if the caller never commits it (e.g. it throws before
    // CommitVaultRekeyAsync) - the live vault is untouched until the commit step runs.
    public Task<string> PrepareVaultRekeyAsync(string newPasswordBase64)
    {
        lock (_dbLock)
        {
            // LiteDB 5.0.21's LiteDatabase.Rebuild(new RebuildOptions { Password = ... })
            // throws "Invalid password" whenever the new password differs from the current
            // one (a known upstream bug - litedb-org/LiteDB#1464, #1933, #2449). So instead
            // of rebuilding in place, copy every document into a fresh file created directly
            // under the new password, then swap it in.
            var db = OpenDatabase();
            string tempFile = _databaseFile + ".rekey.tmp";
            if (File.Exists(tempFile)) File.Delete(tempFile);

            using (var newDb = new LiteDatabase(new ConnectionString { Filename = tempFile, Password = newPasswordBase64 }))
            {
                foreach (var collectionName in db.GetCollectionNames())
                {
                    var allDocs = db.GetCollection(collectionName).FindAll().ToList();
                    if (allDocs.Count > 0)
                        newDb.GetCollection(collectionName).InsertBulk(allDocs);
                }

                // Secondary indexes aren't copied by the document-level rebuild above;
                // recreate the ones InitializeDatabaseAsync sets up on "passwords".
                var passwords = newDb.GetCollection<Password>("passwords");
                passwords.EnsureIndex("Category.$id");
                passwords.EnsureIndex(x => x.Tags);
                passwords.EnsureIndex(x => x.SyncVersion);

                var documents = newDb.GetCollection<DocumentAttachment>("documents");
                documents.EnsureIndex("PasswordId");
            }

            return Task.FromResult(tempFile);
        }
    }

    // Atomically swaps a vault produced by PrepareVaultRekeyAsync into place. This is the
    // only step that touches the live vault file, and it's a single File.Replace call rather
    // than a delete-then-move, so an interruption here either leaves the old vault fully
    // intact or completes the swap - never a window where neither file exists.
    public async Task CommitVaultRekeyAsync(string tempFile, string newPasswordBase64)
    {
        lock (_dbLock)
        {
            _databaseInstance?.Dispose();
            _databaseInstance = null;

            string backupFile = _databaseFile + ".rekey.bak";
            File.Replace(tempFile, _databaseFile, backupFile);
            File.Delete(backupFile);

            _encryptionKey = Convert.FromBase64String(newPasswordBase64);
        }
        await Task.CompletedTask;
    }

    public async Task InitializeDatabaseAsync(string masterPassword)
    {
        if (await IsDatabaseInitializedAsync())
            throw new InvalidOperationException("Database is already initialized");

        if (string.IsNullOrEmpty(masterPassword))
            throw new ArgumentException("Master password cannot be empty", nameof(masterPassword));

        byte[] salt = _cryptoService.GenerateRandomBytes(16);
        string hashBase64 = _cryptoService.DeriveKeyFromPassword(masterPassword, salt);
        _encryptionKey = Convert.FromBase64String(hashBase64);
        var user = new User
        {
            Username = Environment.UserName,
            PasswordHash = Convert.ToBase64String(_encryptionKey),
            PasswordSalt = salt,
            BiometricUnlockEnabled = false,
        };
        var db = OpenDatabase();
        // Create collections
        var passwords = db.GetCollection<Password>("passwords");
        var categories = db.GetCollection<Category>("categories");
        var syncDevices = db.GetCollection<SyncDevice>("syncDevices");
        var users = db.GetCollection<User>("users");

        // Create indexes for optimization
        //passwords.EnsureIndex(x => x.Category);
        passwords.EnsureIndex("Category.$id");
        passwords.EnsureIndex(x => x.Tags);
        passwords.EnsureIndex(x => x.SyncVersion);

        var documents = db.GetCollection<DocumentAttachment>("documents");
        documents.EnsureIndex("PasswordId");

        // Create default categories
        if (categories.Count() == 0)
        {
            CreateDefaultCategories(db);
        }
        await SaveUserDataAsync(user);
        DatabaseInitialized?.Invoke(this, EventArgs.Empty);
        _cachedUser = user;
    }
    private static void CreateDefaultCategories(LiteDatabase db)
    {
        var categories = db.GetCollection<Category>("categories");

        var defaultCategories = new[]
        {
            new Category { Name = "Uncategorized", Color = "#9E9E9E", Icon = "fa-solid fa-tag" },
            new Category { Name = "Social Media", Color = "#E53935", Icon = "fa-brands fa-instagram" },
            new Category { Name = "Banking", Color = "#43A047", Icon = "fa-solid fa-building-columns" },
            new Category { Name = "Email", Color = "#1E88E5", Icon = "fa-solid fa-envelope" },
            new Category { Name = "Shopping", Color = "#FB8C00", Icon = "fa-solid fa-shopping-cart" },
            new Category { Name = "Work", Color = "#8E24AA", Icon = "fa-solid fa-briefcase" }
        };

        categories.InsertBulk(defaultCategories);
    }

    public async Task<User?> GetUserAsync()
    {
        if (_cachedUser != null) return _cachedUser;
        if (!File.Exists(_userDataFile)) return null;
        try
        {
            var encryptedData = await File.ReadAllBytesAsync(_userDataFile);
            var jsonString = _cryptoService.Decrypt(encryptedData);
            _cachedUser = System.Text.Json.JsonSerializer.Deserialize<User>(jsonString)!;
            return _cachedUser;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to read user data", ex);
        }
    }

    public async Task UpdateUserAsync(User user)
    {
        _cachedUser = user ?? throw new ArgumentNullException(nameof(user));
        await SaveUserDataAsync(user);
    }

    public Task<bool> IsDatabaseInitializedAsync() =>
         Task.FromResult(File.Exists(_userDataFile));

    public async Task BackupDatabaseAsync(string backupPath)
    {
        if (!await IsDatabaseInitializedAsync())
            throw new InvalidOperationException("Vault not yet initialized.");

        Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
        File.Copy(_databaseFile, backupPath, overwrite: true);
        File.Copy(_userDataFile, $"{backupPath}.user", overwrite: true);
    }

    public static string DefaultBackupFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PasswordManager", "backups");

    public async Task AutoBackupAsync()
    {
        var user = await GetUserAsync();
        if (user == null || !user.AutoBackupEnabled || user.BackupFrequency == BackupFrequency.Manual) return;
        if (!IsBackupDue(user)) return;

        await RunBackupAsync(user);
    }

    public async Task ForceBackupAsync()
    {
        var user = await GetUserAsync();
        if (user == null) return;

        await RunBackupAsync(user);
    }

    private async Task RunBackupAsync(User user)
    {
        string backupFolder = string.IsNullOrWhiteSpace(user.BackupLocation) ? DefaultBackupFolder : user.BackupLocation;
        Directory.CreateDirectory(backupFolder);

        string dateStr = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string backupPath = Path.Combine(backupFolder, $"vault_{dateStr}.db");

        await BackupDatabaseAsync(backupPath);

        // Delete old backups after the retention count
        int retention = Math.Max(1, user.BackupRetentionCount);
        var backups = Directory.GetFiles(backupFolder, "vault_*.db")
            .OrderByDescending(f => File.GetCreationTime(f))
            .ToList();

        foreach (var oldBackup in backups.Skip(retention))
        {
            try
            {
                File.Delete(oldBackup);
                File.Delete($"{oldBackup}.user");
            }
            catch { /* Ignore deletion errors */ }
        }

        user.LastBackupAt = DateTime.UtcNow;
        await UpdateUserAsync(user);
    }

    private static bool IsBackupDue(User user)
    {
        if (user.BackupFrequency == BackupFrequency.OnLogin || user.LastBackupAt == null)
            return true;

        var elapsed = DateTime.UtcNow - user.LastBackupAt.Value;
        return user.BackupFrequency switch
        {
            BackupFrequency.Daily => elapsed >= TimeSpan.FromDays(1),
            BackupFrequency.Weekly => elapsed >= TimeSpan.FromDays(7),
            _ => true
        };
    }

    public async Task RestoreDatabaseAsync(string backupPath, string masterPassword)
    {
        if (!File.Exists(backupPath))
            throw new FileNotFoundException("Backup file not found.", backupPath);

        // validate master password against the .user blob
        string userBlob = $"{backupPath}.user";
        if (!File.Exists(userBlob))
            throw new FileNotFoundException("User metadata file missing in backup.", userBlob);

        var encrypted = await File.ReadAllBytesAsync(userBlob);
        var json = _cryptoService.Decrypt(encrypted);
        var user = System.Text.Json.JsonSerializer.Deserialize<User>(json)!;

        byte[] salt = user.PasswordSalt;
        string hash = _cryptoService.DeriveKeyFromPassword(masterPassword, salt);

        if (!hash.Equals(user.PasswordHash, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Master password does not match backup.");

        lock (_dbLock)
        {
            _databaseInstance?.Dispose();
            _databaseInstance = null;
        }

        // Stage both restored files as temp copies first, so a crash mid-restore leaves the
        // current vault/user.dat pair untouched rather than mixing an old vault with a new
        // user blob (or vice versa).
        string tempDb = _databaseFile + ".restore.tmp";
        string tempUser = _userDataFile + ".restore.tmp";
        File.Copy(backupPath, tempDb, overwrite: true);
        File.Copy(userBlob, tempUser, overwrite: true);

        if (File.Exists(_databaseFile))
        {
            string dbBackup = _databaseFile + ".restore.bak";
            File.Replace(tempDb, _databaseFile, dbBackup);
            File.Delete(dbBackup);
        }
        else
        {
            File.Move(tempDb, _databaseFile);
        }

        if (File.Exists(_userDataFile))
        {
            string userBackup = _userDataFile + ".restore.bak";
            File.Replace(tempUser, _userDataFile, userBackup);
            File.Delete(userBackup);
        }
        else
        {
            File.Move(tempUser, _userDataFile);
        }

        _cachedUser = null;                      // force reload
        _encryptionKey = Convert.FromBase64String(user.PasswordHash);
    }

    // Writes the encrypted user blob to a temp file without touching _userDataFile, so a
    // caller can prepare it ahead of another commit (see PrepareVaultRekeyAsync) and only
    // touch the live file once everything it depends on is ready.
    public async Task<string> PrepareUserDataAsync(User user)
    {
        var jsonString = System.Text.Json.JsonSerializer.Serialize(user);
        var encryptedData = _cryptoService.Encrypt(jsonString);
        string tempFile = _userDataFile + ".tmp";
        await File.WriteAllBytesAsync(tempFile, encryptedData);
        return tempFile;
    }

    // Atomically swaps a blob produced by PrepareUserDataAsync into place.
    public async Task CommitUserDataAsync(string tempFile, User user)
    {
        if (File.Exists(_userDataFile))
        {
            string backupFile = _userDataFile + ".bak";
            File.Replace(tempFile, _userDataFile, backupFile);
            File.Delete(backupFile);
        }
        else
        {
            File.Move(tempFile, _userDataFile);
        }

        _cachedUser = user;
        await Task.CompletedTask;
    }

    private async Task SaveUserDataAsync(User user)
    {
        try
        {
            string tempFile = await PrepareUserDataAsync(user);
            await CommitUserDataAsync(tempFile, user);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to save user data", ex);
        }
    }
}
