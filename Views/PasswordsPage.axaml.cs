using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HotAvalonia;
using PasswordVault.Models;
using PasswordVault.ViewModels;
using ShadUI.Contents;
using ShadUI.Toasts;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Xaml;

namespace PasswordVault.Views;

public partial class PasswordsPage : UserControl
{
    public PasswordsPage()
    {
        InitializeComponent();
        Initialize();
    }
    [AvaloniaHotReload]
    private void Initialize()
    {
        Console.WriteLine("PasswordsPage reloaded at " + DateTime.Now);
        // Re-initialize or refresh logic here
    }
    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }


    private async void CopyTextButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            if (sender is Button button && button.Tag is string text)
            {
                var topLevel = TopLevel.GetTopLevel(this);
                var clipboard = topLevel?.Clipboard;
                if (clipboard == null) return;
                if (DataContext is PasswordListViewModel viewModel)
                {
                    await clipboard.SetTextAsync(text);
                    viewModel._toastManager.CreateToast("Copied to clipboard!")
                                        .WithContent("The selected text has been copied.")
                                        .WithDelay(1)
                                        .Show();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error copying password: {ex.Message}");
            if (DataContext is PasswordListViewModel viewModel)
            {
                viewModel._toastManager.CreateToast("Copy Failed")
                                  .WithContent("An unexpected error occurred while trying to copy the text.")
                                  .WithDelay(2)
                                  .ShowError();
            }
        }

    }

    private async void CopyPasswordButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            if (sender is Button button && button.Tag is Guid passwordId)
            {
                var topLevel = TopLevel.GetTopLevel(this);
                var clipboard = topLevel?.Clipboard;
                if (clipboard == null) return;

                if (DataContext is PasswordListViewModel viewModel)
                {
                    var password = await viewModel.GetPasswordAsync(passwordId);
                    if (password != null)
                    {
                        await clipboard.SetTextAsync(password);
                        viewModel._toastManager.CreateToast("Password copied!")
                          .WithContent("The password has been securely copied to the clipboard.")
                          .WithDelay(1)
                          .Show();
                    }
                    else
                    {
                        viewModel._toastManager.CreateToast("Password Not Found")
                                         .WithContent("Could not retrieve the password. It might be missing or corrupted.")
                                         .WithDelay(1)
                                         .Show();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error copying password: {ex.Message}");
            if (DataContext is PasswordListViewModel viewModel)
            {
                viewModel._toastManager.CreateToast("Copy Failed")
                                      .WithContent("An unexpected error occurred while trying to copy the password.")
                                      .WithDelay(2)
                                      .ShowError();
            }
        }
    }

    private async void PasswordSearch_TextChanged(object? sender, TextChangedEventArgs e)
    {
        try
        {
            if (DataContext is PasswordListViewModel viewModel)
            {
                await viewModel.ExecuteSearchAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Searching Password error : {ex.Message}");
            if (DataContext is PasswordListViewModel viewModel)
            {
                viewModel._toastManager.CreateToast("Searching Failed")
                                      .WithContent("An unexpected error occurred while trying to search.")
                                      .WithDelay(2)
                                      .ShowError();
            }
        }
        finally
        {

        }
    }
}