using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using PasswordVault.ViewModels;

namespace PasswordVault.Views;

public partial class SettingsPage : UserControl
{
    public SettingsPage()
    {
        InitializeComponent();
    }
    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private async void BrowseBackupLocationButton_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null || DataContext is not SettingsViewModel vm) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose Backup Folder",
            AllowMultiple = false
        });

        if (folders == null || folders.Count == 0) return;

        vm.BackupSettingsVM.SetBackupLocationCommand.Execute(folders[0].Path.LocalPath);
    }
}
