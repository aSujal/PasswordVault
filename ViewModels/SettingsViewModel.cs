using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Reflection;
using System.Threading.Tasks;
using PasswordVault.Services.Auth;
using ShadUI;
using Velopack;
using Velopack.Sources;

namespace PasswordVault.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly ThemeWatcher _watcher;

    // exposes the whole color set to the view
    [ObservableProperty] private ThemeColors _currentColors;

    [ObservableProperty] private string _currentVersion = "1.0.0";
    [ObservableProperty] private string _updateStatus = "Check for updates";
    [ObservableProperty] private bool _isChecking;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowCheckButton))]
    private bool _updateAvailable;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowCheckButton))]
    private bool _updateReadyToInstall;

    [ObservableProperty] private int _downloadProgress;

    public bool ShowCheckButton => !UpdateAvailable && !UpdateReadyToInstall;

    private UpdateInfo? _updateInfo;

    public IRelayCommand SetLightCommand { get; }
    public IRelayCommand SetDarkCommand { get; }
    public IRelayCommand SetSystemCommand { get; }
    public IRelayCommand ManageCategoriesCommand { get; }

    public ImportExportViewModel ImportExportVM { get; }
    public BackupSettingsViewModel BackupSettingsVM { get; }
    public AiSettingsViewModel AiSettingsVM { get; }

    private readonly DialogManager _dialogManager;
    private readonly ManageCategoriesViewModel _manageCategoriesViewModel;
    private readonly IAuthService _authService;

    // ── Change Master Password ───────────────────────────────────────
    [ObservableProperty] private string _currentMasterPassword = string.Empty;
    [ObservableProperty] private string _newMasterPassword = string.Empty;
    [ObservableProperty] private string _confirmNewMasterPassword = string.Empty;
    [ObservableProperty] private bool _isChangingPassword;
    [ObservableProperty] private string _passwordChangeStatus = string.Empty;
    [ObservableProperty] private bool _passwordChangeSuccess;
    [ObservableProperty] private bool _hasPasswordChangeResult;

    public SettingsViewModel(
        ThemeWatcher watcher,
        DialogManager dialogManager,
        ManageCategoriesViewModel manageCategoriesViewModel,
        ImportExportViewModel importExportViewModel,
        BackupSettingsViewModel backupSettingsViewModel,
        AiSettingsViewModel aiSettingsViewModel,
        IAuthService authService)
    {
        _watcher = watcher;
        _dialogManager = dialogManager;
        _manageCategoriesViewModel = manageCategoriesViewModel;
        _authService = authService;
        ImportExportVM = importExportViewModel;
        BackupSettingsVM = backupSettingsViewModel;
        AiSettingsVM = aiSettingsViewModel;

        CurrentColors = watcher.ThemeColors;

        _watcher.ThemeChanged += (_, colors) => CurrentColors = colors;

        SetLightCommand = new RelayCommand(() => _watcher.SwitchTheme(ThemeMode.Light));
        SetDarkCommand = new RelayCommand(() => _watcher.SwitchTheme(ThemeMode.Dark));
        SetSystemCommand = new RelayCommand(() => _watcher.SwitchTheme(ThemeMode.System));

        ManageCategoriesCommand = new AsyncRelayCommand(OpenManageCategories);

        var asm = Assembly.GetExecutingAssembly();
        CurrentVersion = asm.GetName().Version?.ToString(3) ?? "1.0.0";
    }

    private async Task OpenManageCategories()
    {
        await _manageCategoriesViewModel.InitializeAsync();
        _dialogManager.CreateDialog(_manageCategoriesViewModel)
            .WithMinWidth(500)
            .Dismissible()
            .Show();
    }

    [RelayCommand]
    private async Task ChangeMasterPassword()
    {
        if (IsChangingPassword) return;

        // Validate inputs
        if (string.IsNullOrWhiteSpace(CurrentMasterPassword))
        {
            PasswordChangeStatus = "Current password cannot be empty.";
            PasswordChangeSuccess = false;
            HasPasswordChangeResult = true;
            return;
        }
        if (string.IsNullOrWhiteSpace(NewMasterPassword) || NewMasterPassword.Length < 8)
        {
            PasswordChangeStatus = "New password must be at least 8 characters.";
            PasswordChangeSuccess = false;
            HasPasswordChangeResult = true;
            return;
        }
        if (NewMasterPassword != ConfirmNewMasterPassword)
        {
            PasswordChangeStatus = "New passwords do not match.";
            PasswordChangeSuccess = false;
            HasPasswordChangeResult = true;
            return;
        }

        IsChangingPassword = true;
        HasPasswordChangeResult = false;
        try
        {
            await _authService.ChangeMasterPasswordAsync(CurrentMasterPassword, NewMasterPassword);
            CurrentMasterPassword = string.Empty;
            NewMasterPassword = string.Empty;
            ConfirmNewMasterPassword = string.Empty;
            PasswordChangeStatus = "Master password changed successfully.";
            PasswordChangeSuccess = true;
            HasPasswordChangeResult = true;
        }
        catch (UnauthorizedAccessException)
        {
            PasswordChangeStatus = "Current password is incorrect.";
            PasswordChangeSuccess = false;
            HasPasswordChangeResult = true;
        }
        catch (Exception ex)
        {
            PasswordChangeStatus = $"Failed to change password: {ex.Message}";
            PasswordChangeSuccess = false;
            HasPasswordChangeResult = true;
        }
        finally
        {
            IsChangingPassword = false;
        }
    }

    [RelayCommand]
    private async Task CheckForUpdates()
    {
        if (IsChecking) return;
        IsChecking = true;
        UpdateStatus = "Checking for updates...";
        UpdateAvailable = false;
        UpdateReadyToInstall = false;

        try
        {
            var source = new GithubSource("https://github.com/aSujal/PasswordVault", accessToken: null, prerelease: false);
            var mgr = new UpdateManager(source);

            _updateInfo = await mgr.CheckForUpdatesAsync();
            if (_updateInfo == null)
            {
                UpdateStatus = "Application is up to date.";
            }
            else
            {
                UpdateStatus = $"Update available: v{_updateInfo.TargetFullRelease.Version}";
                UpdateAvailable = true;
            }
        }
        catch (Exception ex)
        {
            if (ex.GetType().Name == "NotInstalledException" || ex.Message.Contains("locator"))
            {
                UpdateStatus = "Updates are only available in the installed version.";
            }
            else
            {
                UpdateStatus = $"Failed to check for updates: {ex.Message}";
            }
        }
        finally
        {
            IsChecking = false;
        }
    }

    [RelayCommand]
    private async Task DownloadUpdate()
    {
        if (_updateInfo == null || IsChecking) return;
        IsChecking = true;
        UpdateStatus = "Downloading update...";
        DownloadProgress = 0;

        try
        {
            var source = new GithubSource("https://github.com/aSujal/PasswordVault", accessToken: null, prerelease: false);
            var mgr = new UpdateManager(source);

            await mgr.DownloadUpdatesAsync(_updateInfo, (progress) =>
            {
                DownloadProgress = progress;
                UpdateStatus = $"Downloading update ({progress}%)...";
            });

            UpdateStatus = "Update downloaded. Ready to install.";
            UpdateAvailable = false;
            UpdateReadyToInstall = true;
        }
        catch (Exception ex)
        {
            UpdateStatus = $"Failed to download update: {ex.Message}";
        }
        finally
        {
            IsChecking = false;
        }
    }

    [RelayCommand]
    private void RestartToApply()
    {
        if (_updateInfo == null) return;

        try
        {
            var source = new GithubSource("https://github.com/aSujal/PasswordVault", accessToken: null, prerelease: false);
            var mgr = new UpdateManager(source);
            mgr.ApplyUpdatesAndRestart(_updateInfo.TargetFullRelease);
        }
        catch (Exception ex)
        {
            UpdateStatus = $"Failed to apply update: {ex.Message}";
        }
    }
}
