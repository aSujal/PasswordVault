using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using PasswordVault.ViewModels;

namespace PasswordVault.Views;

public partial class DocumentPreviewDialog : UserControl
{
    public DocumentPreviewDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private async void SaveAsButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not DocumentPreviewViewModel vm || vm.Document == null) return;

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Document As",
                SuggestedFileName = vm.Document.FileName,
            });

            if (file == null) return;

            await vm.SaveAsAsync(file.Path.LocalPath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Save As error: {ex.Message}");
        }
    }
}
