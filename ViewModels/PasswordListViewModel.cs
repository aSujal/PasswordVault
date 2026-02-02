using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordVault.Models;
using PasswordVault.Services;
using PasswordVault.Services.Auth;
using PasswordVault.Services.Database;
using ShadUI;
using Windows.ApplicationModel.DataTransfer;

namespace PasswordVault.ViewModels;

public partial class PasswordListViewModel : ViewModelBase
{
    private readonly DialogManager _dialogManager;
    private readonly AddPasswordDialogViewModel _addPasswordViewModel;
    private readonly IPasswordService _passwordService;
    private readonly IAuthService _authService;
    public readonly ToastManager _toastManager;

    [ObservableProperty]
    private ObservableCollection<Password> _passwords = new();

    [ObservableProperty]
    private string? _searchText;

    [ObservableProperty]
    private bool _isSearching;

    public PasswordListViewModel(
        DialogManager dialogManager,
        AddPasswordDialogViewModel addPasswordViewModel,
        IPasswordService passwordService,
        IAuthService authService,
        ToastManager toastManager
        )
    {
        _dialogManager = dialogManager;
        _addPasswordViewModel = addPasswordViewModel;
        _passwordService = passwordService;
        _authService = authService;
        _toastManager = toastManager;
        _authService.Authenticated += OnAuthenticated;
        _addPasswordViewModel.PasswordAddedSuccessfully += async (s, e) => await RefreshAsync();
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
            Passwords = new ObservableCollection<Password>(allPasswords);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading passwords: {ex.Message}");
            Passwords = new ObservableCollection<Password>();
        }
        finally
        {
            IsSearching = false;
        }
    }

    public async Task ExecuteSearchAsync()
    {
        try
        {
            var searchTerm = SearchText;
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                var allPasswords = await _passwordService.GetAllPasswordsAsync();
                Passwords = new ObservableCollection<Password>(allPasswords);
            }
            else
            {
                var result = await _passwordService.SearchPasswordsAsync(searchTerm);
                Passwords = new ObservableCollection<Password>(result);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error searching passwords: {ex.Message}");
            Passwords = new ObservableCollection<Password>();
        }
        finally
        {
            IsSearching = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsSearching = true;
        SearchText = string.Empty;
        try
        {
            var allPasswords = await _passwordService.GetAllPasswordsAsync();
            Passwords = new ObservableCollection<Password>(allPasswords);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error refreshing passwords: {ex.Message}");
            Passwords = new ObservableCollection<Password>();
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
    private async Task EditPasswordAsync(Password? passwordToEdit)
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

        // You might want to add a confirmation dialog here
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
    public async Task ConfirmDeletePasswordAsync(Password? passwordToDelete)
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
