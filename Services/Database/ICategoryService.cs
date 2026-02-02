using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PasswordVault.Models;

namespace PasswordVault.Services.Database;

public interface ICategoryService
{
    Task<IEnumerable<Category>> GetAllCategoriesAsync();
    Task<Category> GetCategoryByIdAsync(Guid id);
    Task<Category> GetCategoryByNameAsync(string name);
    Task<Category> AddCategoryAsync(Category category);
    Task<Category> UpdateCategoryAsync(Category category);
    Task<bool> DeleteCategoryAsync(Guid id);
    Task<bool> CategoryExistsAsync(string name);
    Task InitializeDefaultCategoriesAsync();
}
