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
using ShadUI;
using Windows.ApplicationModel.DataTransfer;

namespace PasswordVault.ViewModels;

public partial class PasswordListViewModel : ViewModelBase
{
    private readonly DialogManager _dialogManager;
    private readonly AddPasswordDialogViewModel _addPasswordViewModel;
    private readonly IPasswordService _passwordService;
    private readonly IAuthService _authService;
    private readonly ICryptoService _cryptoService;
    private readonly ICategoryService _categoryService;
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

    public PasswordListViewModel(
        DialogManager dialogManager,
        AddPasswordDialogViewModel addPasswordViewModel,
        IPasswordService passwordService,
        IAuthService authService,
        ICryptoService cryptoService,
        ICategoryService categoryService,
        ToastManager toastManager,
        FilterPopupViewModel filterPopupViewModel
        )
    {
        _dialogManager = dialogManager;
        _addPasswordViewModel = addPasswordViewModel;
        _passwordService = passwordService;
        _authService = authService;
        _cryptoService = cryptoService;
        _categoryService = categoryService;
        _toastManager = toastManager;
        _filterViewModel = filterPopupViewModel;
        _authService.Authenticated += OnAuthenticated;
        _addPasswordViewModel.PasswordAddedSuccessfully += async (s, e) => await RefreshAsync();
        _categoryService.CategoriesChanged += async (s, e) => await RefreshAsync();
        _filterViewModel.FiltersApplied += async (s, e) => await ApplyFiltersAsync();
    }

    public void OnAuthenticated(object? sender, EventArgs e)
    {
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
        if (string.IsNullOrEmpty(password.EncryptedPassword)) return;
        try
        {
            var decrypted = _cryptoService.DecryptPassword(password.EncryptedPassword);
            var strength = Helper.PasswordGenerator.EvaluatePasswordStrength(decrypted);
            
            password.StrengthText = strength.Level;
            password.StrengthColor = strength.Score switch
            {
                < 30 => "Red",           // Very Weak
                < 50 => "DarkOrange",    // Weak
                < 70 => "Orange",        // Moderate
                < 90 => "LightGreen",    // Strong
                _ => "Green"             // Very Strong
            };
        }
        catch
        {
            password.StrengthColor = "Transparent";
        }
    }

    public async Task ExecuteSearchAsync()
    {
        try
        {
            var searchTerm = SearchText;
            IEnumerable<Password> passwords;
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
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error searching passwords: {ex.Message}");
            Passwords = [];
        }
        finally
        {
            IsSearching = false;
        }
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
        IsSearching = true;
        SearchText = string.Empty;
        try
        {
            var allPasswords = await _passwordService.GetAllPasswordsAsync();
            foreach (var password in allPasswords) CalculatePasswordStrength(password);
            Passwords = new ObservableCollection<Password>(allPasswords);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error refreshing passwords: {ex.Message}");
            Passwords = [];
            IsSearching = false;
        }
        finally
        {
            IsSearching = false;
        }
    }

    [RelayCommand]
    public void ShowAddPasswordDialog()
    {
        _addPasswordViewModel.Initialize();
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
    private static void CopyUsernameAsync(string? username)
    {
        if (!string.IsNullOrEmpty(username))
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(username);
            Clipboard.SetContent(dataPackage);
            Clipboard.Flush();
        }
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

    public async Task<string?> GetPasswordAsync(Guid? passwordId)
    {
        if (passwordId == null) return null;
        try
        {
            // Fix: Use the Value property of the nullable Guid to pass a non-nullable Guid
            return await _passwordService.GetDecryptedPasswordAsync(passwordId.Value);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error decrypting/copying password: {ex.Message}");
            throw new InvalidOperationException("Failed to retrieve password", ex);
        }
    }
}
