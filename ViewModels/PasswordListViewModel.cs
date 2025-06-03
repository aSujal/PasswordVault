
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordVault.Models;
using PasswordVault.Services;
using PasswordVault.Services.Auth;
using ShadUI.Dialogs;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace PasswordVault.ViewModels;

public partial class PasswordListViewModel : ViewModelBase
{
    private readonly DialogManager _dialogManager;
    private readonly AddPasswordDialogViewModel _addPasswordViewModel;
    private readonly PasswordService _passwordService;
    private readonly AuthService _authService;

    [ObservableProperty]
    private ObservableCollection<Password> _passwords = new();

    [ObservableProperty]
    private string? _searchText;

    [ObservableProperty]
    private bool _isSearching;

    private CancellationTokenSource? _debounceTimerCts;

    public PasswordListViewModel(
        DialogManager dialogManager,
        AddPasswordDialogViewModel addPasswordViewModel,
        PasswordService passwordService,
        AuthService authService
        )
    {
        _dialogManager = dialogManager;
        _addPasswordViewModel = addPasswordViewModel;
        _passwordService = passwordService;
        _authService = authService;
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

    private async Task ExecuteSearchAsync()
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
    private void CopyUsernameAsync(string? username)
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
