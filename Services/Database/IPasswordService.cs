using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PasswordVault.Models;

namespace PasswordVault.Services.Database;

public interface IPasswordService
{
    Task<IEnumerable<Password>> GetAllPasswordsAsync();
    Task<Password> GetPasswordByIdAsync(Guid id);
    Task<IEnumerable<Password>> SearchPasswordsAsync(string searchTerm);
    Task<IEnumerable<Password>> GetPasswordsByCategoryAsync(string categoryName);
    Task<IEnumerable<Password>> GetFavoritePasswordsAsync();
    Task<Password> AddPasswordAsync(Password password);
    Task<Password> UpdatePasswordAsync(Password password);
    Task<bool> DeletePasswordAsync(Guid id);
    Task<string> GetDecryptedPasswordAsync(Guid passwordId);
    Task<bool> UpdateLastUsedAsync(Guid passwordId);
    Task<IEnumerable<Password>> GetRecentPasswordsAsync(int count = 10);
    Task<IEnumerable<Password>> GetPasswordsModifiedSinceAsync(DateTime since);
    Task<int> GetPasswordCountAsync();
    Task<int> GetPasswordCountByCategoryAsync(string categoryName);
}
