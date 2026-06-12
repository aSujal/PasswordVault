using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using PasswordVault.ViewModels;

namespace PasswordVault.Views;

public partial class ImportExportPage : UserControl
{
    public ImportExportPage()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private async void ExportCsvButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Passwords as CSV",
                DefaultExtension = "csv",
                SuggestedFileName = $"PasswordVault_Export_{DateTime.Now:yyyyMMdd}",
                FileTypeChoices =
                [
                    new FilePickerFileType("CSV Files") { Patterns = ["*.csv"] }
                ]
            });

            if (file == null) return;

            if (DataContext is ImportExportViewModel vm)
            {
                await vm.ExportCsvAsync(file.Path.LocalPath);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Export CSV error: {ex.Message}");
        }
    }

    private async void ExportJsonButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Passwords as JSON",
                DefaultExtension = "json",
                SuggestedFileName = $"PasswordVault_Export_{DateTime.Now:yyyyMMdd}",
                FileTypeChoices =
                [
                    new FilePickerFileType("JSON Files") { Patterns = ["*.json"] }
                ]
            });

            if (file == null) return;

            if (DataContext is ImportExportViewModel vm)
            {
                await vm.ExportJsonAsync(file.Path.LocalPath);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Export JSON error: {ex.Message}");
        }
    }

    private async void ImportButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select File to Import",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Supported Files") { Patterns = ["*.csv", "*.json"] },
                    new FilePickerFileType("CSV Files") { Patterns = ["*.csv"] },
                    new FilePickerFileType("JSON Files") { Patterns = ["*.json"] }
                ]
            });

            if (files == null || files.Count == 0) return;

            if (DataContext is ImportExportViewModel vm)
            {
                await vm.ImportAsync(files[0].Path.LocalPath);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Import error: {ex.Message}");
        }
    }
}
