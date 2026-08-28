using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using PasswordVault.Services.Database;

namespace PasswordVault.ViewModels;

public partial class BackupSettingsViewModel : ViewModelBase
{
    private readonly IDatabaseService _databaseService;
    private bool _isLoading;

    [ObservableProperty] private bool _autoBackupEnabled;
    [ObservableProperty] private int _selectedBackupFrequencyIndex;
    [ObservableProperty] private string _backupLocation = string.Empty;
    [ObservableProperty] private int _backupRetentionCount = 7;
    [ObservableProperty] private string _lastBackupText = "Never";
    [ObservableProperty] private string _backupStatus = string.Empty;

    public ObservableCollection<string> BackupFrequencyOptions { get; } =
    [
        "Manual only",
        "Every login",
        "Daily",
        "Weekly"
    ];

    public BackupSettingsViewModel(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
        _ = LoadSettingsAsync();
    }

    private async Task LoadSettingsAsync()
    {
        var user = await _databaseService.GetUserAsync();
        if (user == null) return;

        _isLoading = true;
        AutoBackupEnabled = user.AutoBackupEnabled;
        SelectedBackupFrequencyIndex = (int)user.BackupFrequency;
        BackupLocation = string.IsNullOrWhiteSpace(user.BackupLocation) ? DatabaseService.DefaultBackupFolder : user.BackupLocation;
        BackupRetentionCount = user.BackupRetentionCount;
        LastBackupText = user.LastBackupAt?.ToLocalTime().ToString("g") ?? "Never";
        _isLoading = false;
    }

    partial void OnAutoBackupEnabledChanged(bool value) => SaveSettings();
    partial void OnSelectedBackupFrequencyIndexChanged(int value) => SaveSettings();
    partial void OnBackupRetentionCountChanged(int value) => SaveSettings();

    private void SaveSettings()
    {
        if (_isLoading) return;
        _ = PersistSettingsAsync();
    }

    private async Task PersistSettingsAsync()
    {
        var user = await _databaseService.GetUserAsync();
        if (user == null) return;

        user.AutoBackupEnabled = AutoBackupEnabled;
        user.BackupFrequency = (Models.BackupFrequency)SelectedBackupFrequencyIndex;
        user.BackupRetentionCount = Math.Max(1, BackupRetentionCount);
        user.BackupLocation = BackupLocation == DatabaseService.DefaultBackupFolder ? null : BackupLocation;
        await _databaseService.UpdateUserAsync(user);
    }

    [RelayCommand]
    private void SetBackupLocation(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        BackupLocation = path;
        SaveSettings();
    }

    [RelayCommand]
    private async Task BackupNow()
    {
        try
        {
            await PersistSettingsAsync();
            await _databaseService.ForceBackupAsync();
            var refreshed = await _databaseService.GetUserAsync();
            LastBackupText = refreshed?.LastBackupAt?.ToLocalTime().ToString("g") ?? "Never";
            BackupStatus = "Backup created successfully.";
        }
        catch (Exception ex)
        {
            BackupStatus = $"Backup failed: {ex.Message}";
        }
    }
}
