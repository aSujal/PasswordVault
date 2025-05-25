using LiteDB;
using PasswordVault.Models;
using PasswordVault.Services.Crypto;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PasswordVault.Services.Database;
public interface IDatabaseService
{
    Task InitializeDatabaseAsync(string masterPassword);
    Task<User> GetUserAsync();
    Task UpdateUserAsync(User user);
    Task<bool> IsDatabaseInitializedAsync();
    Task BackupDatabaseAsync(string backupPath);
    Task RestoreDatabaseAsync(string backupPath, string masterPassword);
}

public class DatabaseService : IDatabaseService
{
    private readonly string _databaseFile; // *.db location for LiteDB
    private readonly string _userDataFile; // encrypted JSON settings
    private readonly ICryptoService _cryptoService;

    public event EventHandler? DatabaseInitialized;

    private byte[]? _encryptionKey;
    private User? _cachedUser;
    private bool _isInitialized = false;
    public DatabaseService(ICryptoService cryptoService)
    {
        _cryptoService = cryptoService ?? throw new ArgumentNullException(nameof(cryptoService));

        string appFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PasswordManager");

        _databaseFile = Path.Combine(appFolder, "vault.db");  // LiteDB file
        _userDataFile = Path.Combine(appFolder, "user.dat");  // encrypted user blob

        if (!Directory.Exists(appFolder)) Directory.CreateDirectory(appFolder);
    }

    public LiteDatabase OpenDatabase()
    {
        if (_encryptionKey == null)
            throw new InvalidOperationException("Database not unlocked – call Initialize or supply master password.");

        return new LiteDatabase(new ConnectionString
        {
            Filename = _databaseFile,
            Password = Convert.ToBase64String(_encryptionKey)
        });
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
        using (var db = OpenDatabase())
        {
            // Create collections
            var passwords = db.GetCollection<Password>("passwords");
            var categories = db.GetCollection<Category>("categories");
            var syncDevices = db.GetCollection<SyncDevice>("syncDevices");
            var users = db.GetCollection<User>("users");

            // Create indexes for optimization
            passwords.EnsureIndex(x => x.Category);
            passwords.EnsureIndex(x => x.Tags);
            passwords.EnsureIndex(x => x.SyncVersion);

            // Create default categories
            if (categories.Count() == 0)
            {
                CreateDefaultCategories(db);
            }
        }
        await SaveUserDataAsync(user);
        DatabaseInitialized?.Invoke(this, EventArgs.Empty);
        _cachedUser = user;
    }
    private void CreateDefaultCategories(LiteDatabase db)
    {
        var categories = db.GetCollection<Category>("categories");

        var defaultCategories = new[]
        {
            new Category { Name = "Social Media", Color = "#E53935", Icon = "Social" },
            new Category { Name = "Banking", Color = "#43A047", Icon = "Bank" },
            new Category { Name = "Email", Color = "#1E88E5", Icon = "Email" },
            new Category { Name = "Shopping", Color = "#FB8C00", Icon = "Cart" },
            new Category { Name = "Work", Color = "#8E24AA", Icon = "Work" }
        };

        categories.InsertBulk(defaultCategories);
    }

    public async Task<User> GetUserAsync()
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

        // overwrite local files
        File.Copy(backupPath, _databaseFile, overwrite: true);
        File.Copy(userBlob, _userDataFile, overwrite: true);

        _cachedUser = null;                      // force reload
        _encryptionKey = Convert.FromBase64String(user.PasswordHash);
    }

    private async Task SaveUserDataAsync(User user)
    {
        try
        {
            var jsonString = System.Text.Json.JsonSerializer.Serialize(user);
            var encryptedData = _cryptoService.Encrypt(jsonString);
            await File.WriteAllBytesAsync(_userDataFile, encryptedData);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to save user data", ex);
        }
    }
}
