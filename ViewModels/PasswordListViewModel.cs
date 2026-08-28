using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordVault.Models;
using PasswordVault.Services;
using PasswordVault.Services.Auth;
using PasswordVault.Services.Database;
using PasswordVault.Services.Crypto;
using PasswordVault.Services.AI;
using ShadUI;

namespace PasswordVault.ViewModels;

public partial class PasswordListViewModel : ViewModelBase
{
    private readonly DialogManager _dialogManager;
    private readonly AddPasswordDialogViewModel _addPasswordViewModel;
    private readonly IPasswordService _passwordService;
    private readonly IDocumentService _documentService;
    private readonly IAuthService _authService;
    private readonly ICryptoService _cryptoService;
    private readonly ICategoryService _categoryService;
    private readonly IAiCategorizationService _aiService;
    private readonly AiSettingsService _aiSettingsService;
    public readonly ToastManager _toastManager;

    [ObservableProperty]
    private FilterPopupViewModel _filterViewModel;

    [ObservableProperty]
    private int _activeFilterCount;

    [ObservableProperty]
    private bool _isFilterActive;

    [ObservableProperty]
    private ObservableCollection<Password> _passwords = [];

    [ObservableProperty]
    private string? _searchText;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private bool _isSelectionMode;

    [ObservableProperty]
    private int _selectedCount;

    [ObservableProperty]
    private bool _isAllSelected;

    // AI Categorization State
    [ObservableProperty] private bool _isAiEnabled;
    [ObservableProperty] private bool _isCategorizingPasswords;
    [ObservableProperty] private int _categorizationProgress;

    // Manual bulk categorization
    [ObservableProperty] private ObservableCollection<Category> _bulkCategoryOptions = [];
    [ObservableProperty] private Category? _bulkSelectedCategory;

    public PasswordListViewModel(
        DialogManager dialogManager,
        AddPasswordDialogViewModel addPasswordViewModel,
        IPasswordService passwordService,
        IDocumentService documentService,
        IAuthService authService,
        ICryptoService cryptoService,
        ICategoryService categoryService,
        ToastManager toastManager,
        FilterPopupViewModel filterPopupViewModel,
        IAiCategorizationService aiService,
        AiSettingsService aiSettingsService
        )
    {
        _dialogManager = dialogManager;
        _addPasswordViewModel = addPasswordViewModel;
        _passwordService = passwordService;
        _documentService = documentService;
        _authService = authService;
        _cryptoService = cryptoService;
        _categoryService = categoryService;
        _aiService = aiService;
        _aiSettingsService = aiSettingsService;
        _toastManager = toastManager;
        _filterViewModel = filterPopupViewModel;
        _authService.Authenticated += OnAuthenticated;
        _addPasswordViewModel.PasswordAddedSuccessfully += async (s, e) => await ApplyFiltersAsync();
        _addPasswordViewModel.PasswordUpdatedSuccessfully += async (s, e) => await ApplyFiltersAsync();
        _categoryService.CategoriesChanged += async (s, e) => await ApplyFiltersAsync();
        _filterViewModel.FiltersApplied += async (s, e) => await ApplyFiltersAsync();
    }

    public void OnAuthenticated(object? sender, EventArgs e)
    {
        IsAiEnabled = _aiService.IsConfigured;
        LoadInitialPasswordsAsync();
    }

    private async void LoadInitialPasswordsAsync()
    {
        IsSearching = true;
        try
        {
            var allPasswords = await _passwordService.GetAllPasswordsAsync();
            foreach (var password in allPasswords)
            {
                CalculatePasswordStrength(password);
            }
            Passwords = new ObservableCollection<Password>(allPasswords);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading passwords: {ex.Message}");
            Passwords = [];
        }
        finally
        {
            IsSearching = false;
        }
    }

    private void CalculatePasswordStrength(Password password)
    {
        DecryptLiveTwoFactorSecret(password);

        if (string.IsNullOrEmpty(password.EncryptedPassword)) return;

        Task.Run(() =>
        {
            try
            {
                var decrypted = _cryptoService.DecryptPassword(password.EncryptedPassword);
                var strength = Helper.PasswordGenerator.EvaluatePasswordStrength(decrypted);

                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    password.StrengthText = strength.Level;
                    password.StrengthColor = strength.Score switch
                    {
                        < 30 => "Red",           // Very Weak
                        < 50 => "DarkOrange",    // Weak
                        < 70 => "Orange",        // Moderate
                        < 90 => "LightGreen",    // Strong
                        _ => "Green"             // Very Strong
                    };
                });
            }
            catch
            {
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    password.StrengthColor = "Transparent";
                });
            }
        });
    }

    private void DecryptLiveTwoFactorSecret(Password password)
    {
        password.LiveTwoFactorSecret = string.IsNullOrEmpty(password.TwoFactorSecret)
            ? null
            : _cryptoService.DecryptPassword(password.TwoFactorSecret);
    }

    public async Task ExecuteSearchAsync()
    {
        await ApplyFiltersAsync();
    }

    private async Task ApplyFiltersAsync()
    {
        IsSearching = true;
        try
        {
            IEnumerable<Password> passwords;
            var searchTerm = SearchText;
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                passwords = await _passwordService.GetAllPasswordsAsync();
            }
            else
            {
                passwords = await _passwordService.SearchPasswordsAsync(searchTerm);
            }

            foreach (var password in passwords) CalculatePasswordStrength(password);
            passwords = ApplyInMemoryFilters(passwords);
            Passwords = new ObservableCollection<Password>(passwords);

            IsFilterActive = FilterViewModel.IsAnyFilterActive;
            ActiveFilterCount = CalculateActiveFilterCount();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error applying filters: {ex.Message}");
            Passwords = [];
        }
        finally
        {
            IsSearching = false;
        }
    }

    private IEnumerable<Password> ApplyInMemoryFilters(IEnumerable<Password> passwords)
    {
        var filtered = passwords;

        // Category filter
        if (FilterViewModel.SelectedCategory != null)
        {
            filtered = filtered.Where(p => p.Category?.Id == FilterViewModel.SelectedCategory.Id);
        }

        // Favorites filter
        if (FilterViewModel.ShowFavoritesOnly)
        {
            filtered = filtered.Where(p => p.IsFavorite);
        }

        // Sort
        filtered = FilterViewModel.SelectedSortOption switch
        {
            "Title A-Z" => filtered.OrderBy(p => p.Title),
            "Title Z-A" => filtered.OrderByDescending(p => p.Title),
            "Oldest First" => filtered.OrderBy(p => p.CreatedAt),
            "Last Used" => filtered.OrderByDescending(p => p.LastUsed),
            _ => filtered.OrderByDescending(p => p.CreatedAt) // "Newest First" default
        };

        return filtered;
    }

    private int CalculateActiveFilterCount()
    {
        int count = 0;
        if (FilterViewModel.SelectedCategory != null) count++;
        if (FilterViewModel.ShowFavoritesOnly) count++;
        if (FilterViewModel.SelectedSortOption != "Newest First") count++;
        return count;
    }

    [RelayCommand]
    private async Task OpenFilterPopupAsync()
    {
        await FilterViewModel.LoadCategoriesAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await ApplyFiltersAsync();
    }

    [RelayCommand]
    public async Task ShowAddPasswordDialog()
    {
        await _addPasswordViewModel.InitializeAsync();
        _dialogManager.CreateDialog(_addPasswordViewModel)
            .WithMinWidth(500)
            .WithSuccessCallback(() =>
            {
            })
            .WithCancelCallback(() =>
            {
            })
            .Dismissible()
            .Show();
    }

    [RelayCommand]
    private void EditPassword(Password? passwordToEdit)
    {
        if (passwordToEdit == null) return;

        _addPasswordViewModel.SetPasswordToEdit(passwordToEdit);

        _dialogManager.CreateDialog(_addPasswordViewModel)
            .WithMinWidth(500)
            .WithSuccessCallback(async () =>
            {
                await RefreshAsync();
            })
            .WithCancelCallback(() =>
            {
            })
            .Dismissible()
            .Show();
    }

    [RelayCommand]
    private async Task DuplicatePassword(Password? source)
    {
        if (source == null) return;
        try
        {
            var duplicate = new Password
            {
                Title = source.Title + " (Copy)",
                Username = source.Username,
                EncryptedPassword = source.EncryptedPassword,
                Url = source.Url,
                Notes = source.Notes,
                Category = source.Category,
                Tags = [.. source.Tags],
                IsFavorite = source.IsFavorite,
                TwoFactorSecret = source.TwoFactorSecret,
            };

            await _passwordService.AddPasswordAsync(duplicate);

            var sourceDocuments = await _documentService.GetDocumentsForPasswordAsync(source.Id);
            foreach (var d in sourceDocuments)
            {
                await _documentService.AddDocumentAsync(new DocumentAttachment
                {
                    PasswordId = duplicate.Id,
                    FileName = d.FileName,
                    ContentType = d.ContentType,
                    SizeBytes = d.SizeBytes,
                    Data = d.Data,
                });
            }

            await RefreshAsync();

            _toastManager.CreateToast("Duplicated")
                .WithContent($"'{source.Title}' has been duplicated.")
                .ShowSuccess();
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Duplicate Failed")
                .WithContent($"Could not duplicate entry: {ex.Message}")
                .ShowError();
        }
    }

    [RelayCommand]
    private async Task DeletePassword(Password password)
    {
        if (password == null) return;

        try
        {
            await _passwordService.DeletePasswordAsync(password.Id);
            Passwords.Remove(password);

            _toastManager.CreateToast("Password Deleted")
                .WithContent($"{password.Title} has been deleted.")
                .WithDelay(2)
                .Show();
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Delete Failed")
                .WithContent($"Failed to delete password: {ex.Message}")
                .WithDelay(2)
                .ShowError();
        }
    }

    [RelayCommand]
    public void ConfirmDeletePassword(Password? passwordToDelete)
    {
        if (passwordToDelete == null || passwordToDelete.Id == Guid.Empty) return;

        _dialogManager.CreateDialog("Confirm Deletion", $"Are you sure you want to delete the password entry for '{passwordToDelete.Title}'?")
            .WithPrimaryButton("Continue", async () => await DeletePasswordAsync(passwordToDelete), DialogButtonStyle.Destructive)
            .WithCancelButton("Cancel")
            .Dismissible()
            .Show();
    }

    private async Task DeletePasswordAsync(Password? passwordToDelete)
    {
        try
        {
            if (passwordToDelete == null || passwordToDelete.Id == Guid.Empty) return;
            await _passwordService.DeletePasswordAsync(passwordToDelete.Id);
            await RefreshAsync();
            _toastManager.CreateToast("Deleted").WithContent($"Password '{passwordToDelete.Title}' has been deleted.").ShowSuccess();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting password: {ex.Message}");
            _toastManager.CreateToast("Error").WithContent("Failed to delete password. Please try again").ShowError();
        }
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync(Password? password)
    {
        if (password == null) return;
        password.IsFavorite = !password.IsFavorite;
        try
        {
            await _passwordService.UpdatePasswordAsync(password);
            var index = Passwords.IndexOf(password);
            if (index != -1)
            {
                Passwords[index] = Passwords[index];
            }
        }
        catch (Exception ex)
        {
            password.IsFavorite = !password.IsFavorite;
            Console.WriteLine($"Error updating favorite status: {ex.Message}");
        }
    }

    public async Task<string> GetPasswordAsync(Guid? passwordId)
    {
        if (passwordId == null) return string.Empty;
        try
        {
            return await _passwordService.GetDecryptedPasswordAsync(passwordId.Value);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error decrypting/copying password: {ex.Message}");
            throw new InvalidOperationException("Failed to retrieve password", ex);
        }
    }

    [RelayCommand]
    private void ToggleSelectionMode()
    {
        IsSelectionMode = !IsSelectionMode;
        if (!IsSelectionMode)
        {
            ClearSelections();
        }
    }

    [RelayCommand]
    private void SelectionChanged()
    {
        UpdateSelectedCount();
    }

    [RelayCommand]
    private void SelectAll()
    {
        IsAllSelected = !IsAllSelected;
        foreach (var password in Passwords)
        {
            password.IsSelected = IsAllSelected;
        }
        UpdateSelectedCount();
    }

    public void UpdateSelectedCount()
    {
        SelectedCount = Passwords.Count(p => p.IsSelected);
        IsAllSelected = Passwords.Count > 0 && SelectedCount == Passwords.Count;
    }

    private void ClearSelections()
    {
        foreach (var password in Passwords)
        {
            password.IsSelected = false;
        }
        SelectedCount = 0;
        IsAllSelected = false;
    }

    [RelayCommand]
    private void ConfirmDeleteSelected()
    {
        if (SelectedCount == 0) return;

        _dialogManager.CreateDialog("Confirm Deletion", $"Are you sure you want to delete {SelectedCount} password(s)? This action cannot be undone.")
            .WithPrimaryButton("Delete", async () => await DeleteSelectedAsync(), DialogButtonStyle.Destructive)
            .WithCancelButton("Cancel")
            .Dismissible()
            .Show();
    }

    private async Task DeleteSelectedAsync()
    {
        try
        {
            var selectedIds = Passwords.Where(p => p.IsSelected).Select(p => p.Id).ToList();
            var count = await _passwordService.DeleteMultiplePasswordsAsync(selectedIds);

            IsSelectionMode = false;
            ClearSelections();
            await RefreshAsync();

            _toastManager.CreateToast("Deleted")
                .WithContent($"{count} password(s) deleted successfully.")
                .ShowSuccess();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting passwords: {ex.Message}");
            _toastManager.CreateToast("Error")
                .WithContent("Failed to delete selected passwords. Please try again.")
                .ShowError();
        }
    }

    [RelayCommand]
    private async Task OpenBulkCategorizePopupAsync()
    {
        var categories = await _categoryService.GetAllCategoriesAsync();
        BulkCategoryOptions = new ObservableCollection<Category>(categories);
    }

    [RelayCommand]
    private async Task ApplyBulkCategoryAsync()
    {
        if (BulkSelectedCategory == null) return;

        var targetPasswords = Passwords.Where(p => p.IsSelected).ToList();
        if (targetPasswords.Count == 0) return;

        try
        {
            foreach (var password in targetPasswords)
            {
                password.Category = BulkSelectedCategory;
                await _passwordService.UpdatePasswordAsync(password);
            }

            IsSelectionMode = false;
            ClearSelections();
            BulkSelectedCategory = null;
            await RefreshAsync();

            _toastManager.CreateToast("Categorized")
                .WithContent($"Updated category for {targetPasswords.Count} entries.")
                .ShowSuccess();
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Categorize Failed")
                .WithContent($"Failed to update categories: {ex.Message}")
                .ShowError();
        }
    }

    [RelayCommand]
    private async Task AutoCategorizeSelected()
    {
        if (IsCategorizingPasswords || !_aiService.IsConfigured) return;

        var targetPasswords = Passwords.Where(p => !IsSelectionMode || p.IsSelected).ToList();
        if (targetPasswords.Count == 0) return;

        var aiSettings = await _aiSettingsService.LoadAsync();

        // Privacy warning for cloud providers
        if (aiSettings.Provider != AiProvider.Ollama && !aiSettings.HasUserAcceptedCloudPrivacyWarning)
        {
            _dialogManager.CreateDialog("Privacy Warning",
                "You are using a Cloud AI provider. Title and URL of the selected entries will be sent to the AI. Passwords and Notes are NEVER sent.\n\nDo you want to continue?")
                .WithPrimaryButton("Continue", async () =>
                {
                    aiSettings.HasUserAcceptedCloudPrivacyWarning = true;
                    await _aiSettingsService.SaveAsync(aiSettings);
                    await PerformCategorization(targetPasswords);
                })
                .WithCancelButton("Cancel")
                .Show();

            return;
        }

        await PerformCategorization(targetPasswords);
    }

    private async Task PerformCategorization(List<Password> targetPasswords)
    {
        IsCategorizingPasswords = true;
        CategorizationProgress = 0;
        try
        {
            var categories = await _categoryService.GetAllCategoriesAsync();
            var categoryNames = categories.Select(c => c.Name).ToList();

            var results = await _aiService.BulkSuggestAsync(
                targetPasswords,
                categoryNames,
                progress => CategorizationProgress = progress);

            int updatedCount = 0;
            foreach (var (pw, suggestion) in results)
            {
                var match = categories.FirstOrDefault(c =>
                    c.Name.Equals(suggestion.SuggestedCategory, StringComparison.OrdinalIgnoreCase));

                if (match != null && (pw.Category == null || pw.Category.Id != match.Id))
                {
                    pw.Category = match;
                    await _passwordService.UpdatePasswordAsync(pw);
                    updatedCount++;
                }
            }

            if (IsSelectionMode)
            {
                IsSelectionMode = false;
                ClearSelections();
            }

            await RefreshAsync();

            _toastManager.CreateToast("AI Categorization Complete")
                .WithContent($"Updated categories for {updatedCount} entries.")
                .ShowSuccess();
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("AI Categorization Error")
                .WithContent(ex.Message)
                .ShowError();
        }
        finally
        {
            IsCategorizingPasswords = false;
            CategorizationProgress = 0;
        }
    }
}
