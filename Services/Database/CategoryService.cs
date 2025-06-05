using PasswordVault.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PasswordVault.Services.Database;
public class CategoryService
{
    private readonly DatabaseService _databaseService;
    private readonly string _collectionName = "categories";

    public CategoryService(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task<IEnumerable<Category>> GetAllCategoriesAsync()
    {
        return await Task.Run(() =>
        {
            var db = _databaseService.OpenDatabase();
            var collection = db.GetCollection<Category>(_collectionName);
            return collection.Query()
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.Name)
                .ToList();
        });
    }

    public async Task<Category> GetCategoryByIdAsync(Guid id)
    {
        return await Task.Run(() =>
        {
            var db = _databaseService.OpenDatabase();
            var collection = db.GetCollection<Category>(_collectionName);
            return collection.Query()
                .Where(x => x.Id == id && !x.IsDeleted)
                .FirstOrDefault();
        });
    }

    public async Task<Category> GetCategoryByNameAsync(string name)
    {
        return await Task.Run(() =>
        {
            var db = _databaseService.OpenDatabase();
            var collection = db.GetCollection<Category>(_collectionName);
            return collection.Query()
                .Where(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && !x.IsDeleted)
                .FirstOrDefault();
        });
    }

    public async Task<Category> AddCategoryAsync(Category category)
    {
        return await Task.Run(() =>
        {
            var db = _databaseService.OpenDatabase();
            var collection = db.GetCollection<Category>(_collectionName);

            // Check if category with same name already exists
            var existing = collection.Query()
                .Where(x => x.Name.Equals(category.Name, StringComparison.OrdinalIgnoreCase) && !x.IsDeleted)
                .FirstOrDefault();

            if (existing != null)
                throw new InvalidOperationException($"Category '{category.Name}' already exists");

            category.Id = Guid.NewGuid();
            category.CreatedAt = DateTime.UtcNow;
            category.UpdatedAt = DateTime.UtcNow;
            category.SyncVersion = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            collection.Insert(category);
            return category;
        });
    }

    public async Task<Category> UpdateCategoryAsync(Category category)
    {
        return await Task.Run(() =>
        {
            var db = _databaseService.OpenDatabase();
            var collection = db.GetCollection<Category>(_collectionName);

            var existing = collection.FindById(category.Id);
            if (existing == null || existing.IsDeleted)
                throw new InvalidOperationException("Category not found");

            // Check if another category with same name exists
            var duplicate = collection.Query()
                .Where(x => x.Name.Equals(category.Name, StringComparison.OrdinalIgnoreCase)
                           && x.Id != category.Id && !x.IsDeleted)
                .FirstOrDefault();

            if (duplicate != null)
                throw new InvalidOperationException($"Category '{category.Name}' already exists");

            category.UpdatedAt = DateTime.UtcNow;
            category.SyncVersion = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            collection.Update(category);
            return category;
        });
    }

    public async Task<bool> DeleteCategoryAsync(Guid id)
    {
        return await Task.Run(() =>
        {
            var db = _databaseService.OpenDatabase();
            var collection = db.GetCollection<Category>(_collectionName);

            var category = collection.FindById(id);
            if (category == null || category.IsDeleted)
                return false;

            // Soft delete
            category.IsDeleted = true;
            category.UpdatedAt = DateTime.UtcNow;
            category.SyncVersion = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            collection.Update(category);

            // Move passwords in this category to "Uncategorized"
            var passwordCollection = db.GetCollection<Password>("passwords");
            var passwordsToUpdate = passwordCollection.Query()
                .Where(x => x.Category == category && !x.IsDeleted)
                .ToList();

            foreach (var password in passwordsToUpdate)
            {
                password.UpdatedAt = DateTime.UtcNow;
                password.SyncVersion = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                passwordCollection.Update(password);
            }

            return true;
        });
    }

    public async Task<bool> CategoryExistsAsync(string name)
    {
        return await Task.Run(() =>
        {
            var db = _databaseService.OpenDatabase();
            var collection = db.GetCollection<Category>(_collectionName);
            return collection.Query()
                .Where(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && !x.IsDeleted)
                .Exists();
        });
    }

    public async Task InitializeDefaultCategoriesAsync()
    {
        var defaultCategories = new[]
        {
            new Category { Name = "Uncategorized", Color = "#6B7280", Icon = "Folder" },
            new Category { Name = "Social Media", Color = "#3B82F6", Icon = "Users" },
            new Category { Name = "Email", Color = "#EF4444", Icon = "Mail" },
            new Category { Name = "Banking", Color = "#10B981", Icon = "CreditCard" },
            new Category { Name = "Work", Color = "#F59E0B", Icon = "Briefcase" },
            new Category { Name = "Entertainment", Color = "#8B5CF6", Icon = "Play" },
            new Category { Name = "Shopping", Color = "#EC4899", Icon = "ShoppingCart" },
            new Category { Name = "Gaming", Color = "#F97316", Icon = "Gamepad2" },
            new Category { Name = "Education", Color = "#06B6D4", Icon = "GraduationCap" },
            new Category { Name = "Health", Color = "#84CC16", Icon = "Heart" }
        };

        foreach (var category in defaultCategories)
        {
            if (!await CategoryExistsAsync(category.Name))
            {
                await AddCategoryAsync(category);
            }
        }
    }
}