using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordVault.Models;
using PasswordVault.Services;
using PasswordVault.Services.Auth;
using PasswordVault.Services.Database;
using ShadUI.Dialogs;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace PasswordVault.ViewModels;

public partial class PasswordListViewModel : ViewModelBase
{
    private readonly DialogManager _dialogManager;
    private readonly AddPasswordDialogViewModel _addPasswordViewModel;
    private readonly PasswordService _passwordService;
    private readonly AuthService _authService;
    [ObservableProperty]
    private ObservableCollection<Password> _passwords = new();

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
    }

    public void OnAuthenticated(object? sender, EventArgs e)
    {
        LoadPasswords();
    }

    private async void LoadPasswords()
    {
        var allPasswords = await _passwordService.GetAllPasswordsAsync();
        Passwords = new ObservableCollection<Password>(allPasswords);
    }


    [RelayCommand]
    public void ShowAddPasswordDialogCommand()
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
}
