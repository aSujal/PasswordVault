using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using PasswordVault.ViewModels;
using ShadUI;

namespace PasswordVault.Views;

public partial class DocumentsPage : UserControl
{
    public DocumentsPage()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private async void AddDocumentButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null || DataContext is not DocumentListViewModel viewModel) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Documents to Add",
                AllowMultiple = true,
            });

            if (files == null || files.Count == 0) return;

            await viewModel.AddFilesAsync([.. files.Select(f => f.Path.LocalPath)]);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Add document error: {ex.Message}");
            if (DataContext is DocumentListViewModel viewModel)
            {
                viewModel._toastManager.CreateToast("Add Failed")
                    .WithContent("An unexpected error occurred while adding documents.")
                    .ShowError();
            }
        }
    }

    private async void DocumentSearch_TextChanged(object? sender, TextChangedEventArgs e)
    {
        try
        {
            if (DataContext is DocumentListViewModel viewModel)
            {
                await viewModel.ExecuteSearchAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Searching documents error: {ex.Message}");
        }
    }
}
