using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordVault.Models;
using PasswordVault.Services;
using PasswordVault.Services.Auth;
using PasswordVault.Services.Crypto;
using PasswordVault.Services.Database;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace PasswordVault.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly PasswordService _passwordService;
    private readonly CategoryService _categoryService;
    private readonly AuthService _authService;
    private readonly ICryptoService _cryptoService;

    [ObservableProperty]
    private int _totalPasswords;

    [ObservableProperty]
    private int _favoritePasswords;

    [ObservableProperty]
    private int _weakPasswords;

    [ObservableProperty]
    private int _totalCategories;

    [ObservableProperty]
    private ObservableCollection<Password> _recentPasswords = new();

    [ObservableProperty]
    private ObservableCollection<CategoryStats> _categoryStats = new();

    [ObservableProperty]
    private bool _isLoading;

    public DashboardViewModel(
        PasswordService passwordService,
        ICryptoService cryptoService,
        CategoryService categoryService,
        AuthService authService)
    {
        _passwordService = passwordService ?? throw new ArgumentNullException(nameof(passwordService));
        _cryptoService = cryptoService ?? throw new ArgumentNullException(nameof(cryptoService));
        _categoryService = categoryService ?? throw new ArgumentNullException(nameof(categoryService));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));

        _authService.Authenticated += OnAuthenticated;
    }

    private void OnAuthenticated(object? sender, EventArgs e)
    {
        _ = LoadDashboardDataAsync();
    }

    [RelayCommand]
    private async Task LoadDashboardDataAsync()
    {
        IsLoading = true;
        try
        {
            var passwords = await _passwordService.GetAllPasswordsAsync();
            var categories = await _categoryService.GetAllCategoriesAsync();

            TotalPasswords = passwords.Count();
            FavoritePasswords = passwords.Count(p => p.IsFavorite);
            TotalCategories = categories.Count();

            // Calculate weak passwords
            WeakPasswords = passwords.Count(p =>
            {
                var decrypted = _cryptoService.DecryptPassword(p.EncryptedPassword);
                var strength = Helper.PasswordGenerator.EvaluatePasswordStrength(decrypted);
                return strength.Score <= 2; // Weak or Very Weak
            });

            // Get recent passwords (last 5)
            RecentPasswords = new ObservableCollection<Password>(
                passwords.OrderByDescending(p => p.CreatedAt).Take(5));

            // Calculate category statistics
            var stats = categories.Select(c => new CategoryStats
            {
                Category = c,
                PasswordCount = passwords.Count(p => p.Category?.Id == c.Id)
            }).OrderByDescending(s => s.PasswordCount);

            CategoryStats = new ObservableCollection<CategoryStats>(stats);
        }
        catch (Exception ex)
        {
            // Handle error
            System.Diagnostics.Debug.WriteLine($"Error loading dashboard: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }
}

public class CategoryStats
{
    public Category Category { get; set; } = null!;
    public int PasswordCount { get; set; }
}