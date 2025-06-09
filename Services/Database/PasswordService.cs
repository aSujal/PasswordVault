using PasswordVault.Models;
using PasswordVault.Services.Crypto;
using PasswordVault.Services.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PasswordVault.Services;

public class PasswordService
{
    private readonly DatabaseService _databaseService;
    private readonly ICryptoService _cryptoService;
    private readonly string _collectionName = "passwords";
    private static readonly SemaphoreSlim _databaseAccessSemaphore = new SemaphoreSlim(1, 1);
    public PasswordService(DatabaseService databaseService, ICryptoService cryptoService)
    {
        _databaseService = databaseService;
        _cryptoService = cryptoService;
    }

    public async Task<IEnumerable<Password>> GetAllPasswordsAsync()
    {

        return await Task.Run(() =>
        {
            var db = _databaseService.OpenDatabase();
            var collection = db.GetCollection<Password>(_collectionName);
            return collection.Query()
                .Include(x => x.Category)
                .Where(x => !x.IsDeleted)
                .OrderByDescending(x => x.IsFavorite)
                .ToList();
        });
    }

    public async Task<Password> GetPasswordByIdAsync(Guid id)
    {
        return await Task.Run(() =>
        {
            var db = _databaseService.OpenDatabase();
            var collection = db.GetCollection<Password>(_collectionName);
            return collection.Query()
                .Include(x => x.Category)
                .Where(x => x.Id == id && !x.IsDeleted)
                .FirstOrDefault();
        });
    }

    public async Task<IEnumerable<Password>> SearchPasswordsAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return await GetAllPasswordsAsync();

        return await Task.Run(() =>
        {
            var db = _databaseService.OpenDatabase();
            var collection = db.GetCollection<Password>(_collectionName);

            var searchLower = searchTerm.ToLowerInvariant();

            var dbFiltered = collection.Query()
                .Include(x => x.Category)
                .Where(x => !x.IsDeleted && (
                    x.Title.ToLower().Contains(searchLower) ||
                    (x.Username != null && x.Username.ToLower().Contains(searchLower)) ||
                    (x.Url != null && x.Url.ToLower().Contains(searchLower)) ||
                    (x.Notes != null && x.Notes.ToLower().Contains(searchLower)) ||
                    (x.Category != null && x.Category.Name.ToLower().Contains(searchLower))
                ))
                .OrderByDescending(x => x.IsFavorite)
                .ToList();
            return dbFiltered;
        });
    }

    public async Task<IEnumerable<Password>> GetPasswordsByCategoryAsync(string categoryName)
    {
        return await Task.Run(() =>
        {
            var db = _databaseService.OpenDatabase();
            var collection = db.GetCollection<Password>(_collectionName);
            return collection.Query()
                .Include(x => x.Category)
                .Where(x => !x.IsDeleted && x.Category != null && x.Category.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.IsFavorite)
                .ToList();
        });
    }

    public async Task<IEnumerable<Password>> GetFavoritePasswordsAsync()
    {
        return await Task.Run(() =>
        {
            var db = _databaseService.OpenDatabase();
            var collection = db.GetCollection<Password>(_collectionName);
            return collection.Query()
                .Include(x => x.Category)
                .Where(x => !x.IsDeleted && x.IsFavorite)
                .OrderByDescending(x => x.LastUsed)
                .ToList();
        });
    }

    public async Task<Password> AddPasswordAsync(Password password)
    {
        if (password.Category == null)
        {
            throw new ArgumentNullException(nameof(password.Category), "Password must have a category assigned.");
        }
        return await Task.Run(() =>
        {
            var db = _databaseService.OpenDatabase();
            var collection = db.GetCollection<Password>(_collectionName);

            password.Id = Guid.NewGuid();
            password.CreatedAt = DateTime.UtcNow;
            password.UpdatedAt = DateTime.UtcNow;
            password.LastUsed = DateTime.UtcNow;
            password.SyncVersion = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            collection.Insert(password);
            return password;
        });
    }

    public async Task<Password> UpdatePasswordAsync(Password password)
    {
        return await Task.Run(() =>
        {
            var db = _databaseService.OpenDatabase();
            var collection = db.GetCollection<Password>(_collectionName);

            var existing = collection.FindById(password.Id);
            if (existing == null || existing.IsDeleted)
                throw new InvalidOperationException("Password not found or has been deleted.");

            password.UpdatedAt = DateTime.UtcNow;
            password.SyncVersion = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            password.CreatedAt = existing.CreatedAt;

            collection.Update(password);
            return password;
        });
    }

    public async Task<bool> DeletePasswordAsync(Guid id)
    {
        return await Task.Run(() =>
        {
            var db = _databaseService.OpenDatabase();
            var collection = db.GetCollection<Password>(_collectionName);

            var password = collection.FindById(id);
            if (password == null || password.IsDeleted)
                return false;

            // Soft delete
            password.IsDeleted = true;
            password.UpdatedAt = DateTime.UtcNow;
            password.SyncVersion = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            collection.Update(password);
            return true;
        });
    }

    public async Task<string> GetDecryptedPasswordAsync(Guid passwordId)
    {
        var password = await GetPasswordByIdAsync(passwordId);
        if (password == null)
            throw new InvalidOperationException("Password not found");

        return _cryptoService.DecryptPassword(password.EncryptedPassword);
    }

    public async Task<bool> UpdateLastUsedAsync(Guid passwordId)
    {
        return await Task.Run(() =>
        {
            var db = _databaseService.OpenDatabase();
            var collection = db.GetCollection<Password>(_collectionName);

            var password = collection.FindById(passwordId);
            if (password == null || password.IsDeleted)
                return false;

            password.LastUsed = DateTime.UtcNow;
            password.UpdatedAt = DateTime.UtcNow;
            password.SyncVersion = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            collection.Update(password);
            return true;
        });
    }

    public async Task<IEnumerable<Password>> GetRecentPasswordsAsync(int count = 10)
    {
        return await Task.Run(() =>
        {
            var db = _databaseService.OpenDatabase();
            var collection = db.GetCollection<Password>(_collectionName);
            return collection.Query()
                .Include(x => x.Category)
                .Where(x => !x.IsDeleted)
                .OrderByDescending(x => x.LastUsed)
                .Limit(count)
                .ToList();
        });
    }

    public async Task<IEnumerable<Password>> GetPasswordsModifiedSinceAsync(DateTime since)
    {
        return await Task.Run(() =>
        {
            var db = _databaseService.OpenDatabase();
            var collection = db.GetCollection<Password>(_collectionName);
            return collection.Query()
                .Include(x => x.Category)
                .Where(x => x.UpdatedAt > since)
                .OrderByDescending(x => x.UpdatedAt)
                .ToList();
        });
    }

    public async Task<int> GetPasswordCountAsync()
    {
        return await Task.Run(() =>
        {
            var db = _databaseService.OpenDatabase();
            var collection = db.GetCollection<Password>(_collectionName);
            return collection.Query()
                .Where(x => !x.IsDeleted)
                .Count();
        });
    }

    public async Task<int> GetPasswordCountByCategoryAsync(string categoryName)
    {
        return await Task.Run(() =>
        {
            var db = _databaseService.OpenDatabase();
            var collection = db.GetCollection<Password>(_collectionName);
            return collection.Query()
             .Include(x => x.Category)
             .Where(x => !x.IsDeleted && x.Category != null && x.Category.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase))
             .Count();
        });
    }
}