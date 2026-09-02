using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using PasswordVault.ViewModels;
using ShadUI;
using System;

namespace PasswordVault.Views;

public partial class PasswordsPage : UserControl
{
    private DispatcherTimer? _clipboardClearTimer;

    public PasswordsPage()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Starts (or resets) the 30-second clipboard auto-clear timer.
    /// </summary>
    private void ScheduleClipboardClear(IClipboard clipboard, PasswordListViewModel viewModel)
    {
        // Cancel any existing timer so back-to-back copies reset the countdown.
        _clipboardClearTimer?.Stop();
        _clipboardClearTimer = null;

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        timer.Tick += async (_, _) =>
        {
            timer.Stop();
            _clipboardClearTimer = null;
            await clipboard.ClearAsync();
            viewModel._toastManager
                .CreateToast("Clipboard cleared")
                .WithContent("Your copied password has been cleared from the clipboard.")
                .WithDelay(3)
                .Show();
        };
        _clipboardClearTimer = timer;
        timer.Start();
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
                          .WithContent("Clipboard will be cleared automatically in 30 seconds.")
                          .WithDelay(3)
                          .Show();

                        // Schedule auto-clear after 30 seconds
                        ScheduleClipboardClear(clipboard, viewModel);
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

    private async void CopyTotpButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            if (sender is not Button button || button.Tag is not string code || string.IsNullOrWhiteSpace(code)) return;

            var topLevel = TopLevel.GetTopLevel(this);
            var clipboard = topLevel?.Clipboard;
            if (clipboard == null || DataContext is not PasswordListViewModel viewModel) return;

            await clipboard.SetTextAsync(code);
            viewModel._toastManager.CreateToast("2FA code copied!")
                .WithContent($"Code {code} valid for {PasswordVault.Services.Totp.TotpService.GetSecondsRemaining()}s.")
                .WithDelay(3)
                .Show();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error copying 2FA code: {ex.Message}");
            if (DataContext is PasswordListViewModel viewModel)
            {
                viewModel._toastManager.CreateToast("Copy Failed")
                                  .WithContent("Could not generate the 2FA code. Check the secret key.")
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

    private async void FilterButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            if (DataContext is PasswordListViewModel viewModel)
            {
                await viewModel.OpenFilterPopupCommand.ExecuteAsync(null);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error opening filter popup: {ex.Message}");
        }
    }

}