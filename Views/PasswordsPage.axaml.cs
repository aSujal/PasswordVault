using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using HotAvalonia;
using PasswordVault.ViewModels;
using ShadUI.Contents;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

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


    private async void CopyTextButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is Button button && button.Tag is string text)
            {
                var topLevel = TopLevel.GetTopLevel(this);
                var clipboard = topLevel?.Clipboard;
                if (clipboard == null) return;

                await clipboard.SetTextAsync(text);
                //ShowCopyNotification();
                //_clipboardIcon.Data = Icons.Check;
                //_timer.Stop();
                //_timer.Start();
            }
        }
        catch (Exception)
        {
            //ignore
        }

    }

    private async void CopyPasswordButton_Click(object? sender, RoutedEventArgs e)
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
                        //ShowCopyNotification();
                        //_clipboardIcon.Data = Icons.Check;
                        //_timer.Stop();
                        //_timer.Start();
                    }
                }
            }
        }
        catch (Exception)
        {
            //ignore
        }

    }
}