using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordVault.Helper;
using PasswordVault.Models;
using PasswordVault.Services.Auth;
using PasswordVault.Services.Crypto;
using PasswordVault.Services.Database;

namespace PasswordVault.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private const int DotCount = 24;

    private readonly IPasswordService _passwordService;
    private readonly ICategoryService _categoryService;
    private readonly IDocumentService _documentService;
    private readonly IDocumentFolderService _folderService;
    private readonly ICryptoService _cryptoService;

    [ObservableProperty] private int _totalPasswords;
    [ObservableProperty] private int _favoritePasswords;
    [ObservableProperty] private int _weakPasswords;
    [ObservableProperty] private int _reusedPasswords;
    [ObservableProperty] private int _healthyPasswords;
    [ObservableProperty] private int _twoFactorCount;
    [ObservableProperty] private int _documentCount;
    [ObservableProperty] private int _folderCount;

    // Share of passwords that are neither weak nor reused, drawn as the ring on the health card.
    [ObservableProperty] private int _healthScore;
    [ObservableProperty] private double _healthSweep;
    [ObservableProperty] private string _healthColor = "#22C55E";
    [ObservableProperty] private string _healthLabel = string.Empty;

    // One opacity per dot: the two-factor card fills a fixed row of dots by coverage, so it
    // reads the same for 10 passwords or 1000.
    [ObservableProperty] private ObservableCollection<double> _twoFactorDots = [];
    [ObservableProperty] private int _twoFactorPercent;

    [ObservableProperty] private ObservableCollection<StrengthBucket> _strengthBuckets = [];
    [ObservableProperty] private ObservableCollection<Password> _recentPasswords = [];
    [ObservableProperty] private ObservableCollection<CategoryStats> _categoryStats = [];

    public string Today => DateTime.Now.ToString("dddd, MMMM d");

    public DashboardViewModel(
        IPasswordService passwordService,
        ICryptoService cryptoService,
        ICategoryService categoryService,
        IDocumentService documentService,
        IDocumentFolderService folderService,
        IAuthService authService)
    {
        _passwordService = passwordService;
        _cryptoService = cryptoService;
        _categoryService = categoryService;
        _documentService = documentService;
        _folderService = folderService;

        authService.Authenticated += (_, _) => _ = LoadDashboardDataAsync();
    }

    [RelayCommand]
    private async Task LoadDashboardDataAsync()
    {
        try
        {
            var passwords = (await _passwordService.GetAllPasswordsAsync()).ToList();
            var categories = await _categoryService.GetAllCategoriesAsync();
            var documents = await _documentService.GetDocumentsAsync(new DocumentQuery());
            var folders = await _folderService.GetAllFoldersAsync();

            var plaintexts = passwords.Select(p => TryDecrypt(p.EncryptedPassword)).ToList();
            var levels = plaintexts.Select(p => p == null ? null : PasswordGenerator.EvaluatePasswordStrength(p).Level).ToList();
            var reusedTexts = plaintexts.Where(p => !string.IsNullOrEmpty(p))
                .GroupBy(p => p).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();

            bool IsWeak(string? level) => level is "Weak" or "Very Weak";

            TotalPasswords = passwords.Count;
            FavoritePasswords = passwords.Count(p => p.IsFavorite);
            WeakPasswords = levels.Count(IsWeak);
            ReusedPasswords = plaintexts.Count(p => p != null && reusedTexts.Contains(p));
            HealthyPasswords = plaintexts.Where((p, i) => p != null && !IsWeak(levels[i]) && !reusedTexts.Contains(p)).Count();
            DocumentCount = documents.Count;
            FolderCount = folders.Count;

            HealthScore = TotalPasswords == 0 ? 0 : (int)Math.Round(100.0 * HealthyPasswords / TotalPasswords);
            HealthSweep = 3.6 * HealthScore;
            (HealthColor, HealthLabel) = HealthScore switch
            {
                >= 80 => ("#22C55E", "Looking good"),
                >= 50 => ("#F59E0B", "Room to improve"),
                _ => ("#EF4444", "Needs attention"),
            };

            TwoFactorCount = passwords.Count(p => !string.IsNullOrEmpty(p.TwoFactorSecret));
            TwoFactorPercent = TotalPasswords == 0 ? 0 : (int)Math.Round(100.0 * TwoFactorCount / TotalPasswords);
            var filled = (int)Math.Round(DotCount * TwoFactorPercent / 100.0);
            TwoFactorDots = new ObservableCollection<double>(Enumerable.Range(0, DotCount).Select(i => i < filled ? 1.0 : 0.15));

            (string Level, string Color)[] order =
                [("Very Strong", "#22C55E"), ("Strong", "#84CC16"), ("Moderate", "#F59E0B"), ("Weak", "#F97316"), ("Very Weak", "#EF4444")];
            StrengthBuckets = new ObservableCollection<StrengthBucket>(order.Select(o =>
            {
                var count = levels.Count(l => l == o.Level);
                return new StrengthBucket(o.Level, count, TotalPasswords == 0 ? 0 : (double)count / TotalPasswords, o.Color);
            }));

            RecentPasswords = new ObservableCollection<Password>(passwords.OrderByDescending(p => p.CreatedAt).Take(5));

            var counts = categories
                .Select(c => (Category: c, Count: passwords.Count(p => p.Category?.Id == c.Id)))
                .Where(s => s.Count > 0)
                .OrderByDescending(s => s.Count)
                .ToList();
            var max = counts.Count == 0 ? 1 : counts.Max(s => s.Count);
            CategoryStats = new ObservableCollection<CategoryStats>(
                counts.Take(5).Select(s => new CategoryStats(s.Category, s.Count, (double)s.Count / max)));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading dashboard: {ex.Message}");
        }
    }

    private string? TryDecrypt(string encrypted)
    {
        if (string.IsNullOrEmpty(encrypted)) return null;
        try { return _cryptoService.DecryptPassword(encrypted); }
        catch { return null; }
    }
}

public record CategoryStats(Category Category, int PasswordCount, double Fraction);

public record StrengthBucket(string Label, int Count, double Fraction, string Color);
