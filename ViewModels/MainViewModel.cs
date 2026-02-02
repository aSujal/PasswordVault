using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using PasswordVault.Models;
using PasswordVault.Services.Auth;
using PasswordVault.Services.Database;
using PasswordVault.Services.Sync;
using PasswordVault.Validators;
using ShadUI;

namespace PasswordVault.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty] private ViewModelBase _currentViewModel;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private bool _isAuthenticated;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isSyncing;
    [ObservableProperty] private bool _isSidebarExpanded = false;
    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private bool _isCreatingNewVault;
    [ObservableProperty] private string _actionButtonText = "Login";
    [ObservableProperty] private ToastManager _toastManager;
    [ObservableProperty] private DialogManager _dialogManager;
    private string _confirmMasterPassword = string.Empty;
    private string _masterPassword = "12345678";
    [Required]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters long")]
    public string MasterPassword
    {
        get => _masterPassword;
        set => SetProperty(ref _masterPassword, value);
    }

    [Required]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters long")]
    [IsMatchWith(nameof(MasterPassword), ErrorMessage = "Passwords do not match")]
    public string ConfirmMasterPassword
    {
        get => _confirmMasterPassword;
        set => SetProperty(ref _confirmMasterPassword, value);
    }

    public string Greeting { get; } = "Welcome to Avalonia!";
    [ObservableProperty] private bool _passwordsMatch = true;

    public PasswordListViewModel PasswordListVM { get; }
    public DashboardViewModel DashboardVM { get; }
    public SettingsViewModel SettingsVM { get; }
    public SyncViewModel SyncVM { get; }

    public ICommand NavigateToPasswordsCommand { get; }
    public ICommand NavigateToDashboardCommand { get; }
    public ICommand NavigateToSettingsCommand { get; }
    public ICommand NavigateToSyncCommand { get; }
    public ICommand LockCommand { get; }
    public ICommand LoginCommand { get; }

    private readonly IAuthService _authService;
    private readonly SyncService _syncService;
    private readonly DatabaseService _databaseService;

    public MainViewModel(IAuthService authService, SyncService syncService, DatabaseService databaseService, IServiceProvider provider, DialogManager dialogManager, ToastManager toastManager)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
        _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
        _dialogManager = dialogManager ?? throw new ArgumentNullException(nameof(dialogManager));
        _toastManager = toastManager ?? throw new ArgumentNullException(nameof(toastManager));

        PasswordListVM = provider.GetRequiredService<PasswordListViewModel>();
        DashboardVM = provider.GetRequiredService<DashboardViewModel>();
        SettingsVM = provider.GetRequiredService<SettingsViewModel>();
        SyncVM = provider.GetRequiredService<SyncViewModel>();

        Title = "Password Vault";

        NavigateToPasswordsCommand = new RelayCommand(() => NavigateTo(PasswordListVM));
        NavigateToDashboardCommand = new RelayCommand(() => NavigateTo(DashboardVM));
        NavigateToSettingsCommand = new RelayCommand(() => NavigateTo(SettingsVM));
        NavigateToSyncCommand = new RelayCommand(() => NavigateTo(SyncVM));

        LockCommand = new AsyncRelayCommand(LockApplicationAsync);
        LoginCommand = new AsyncRelayCommand(HandleLoginAsync);

        CurrentViewModel = DashboardVM;

        _authService.Authenticated += OnAuthenticated;
        _authService.Locked += OnLocked;
        //_syncService.Authenticated += OnSyncStateChanged;
    }

    public async Task InitializeAsync()
    {
        IsBusy = true;
        StatusMessage = "Checking database status...";
        try
        {
            bool isInitialized = await _databaseService.IsDatabaseInitializedAsync();
            IsCreatingNewVault = !isInitialized;
            ActionButtonText = !isInitialized ? "Create Vault" : "Login";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error checking database: {ex.Message}";
        }
        finally
        {
            StatusMessage = "";
            IsBusy = false;
        }
    }

    private void NavigateTo(ViewModelBase target)
    {
        if (CurrentViewModel == target) return;

        CurrentViewModel = target;
    }
    private async Task LockApplicationAsync()
    {
        IsBusy = true;
        StatusMessage = "Locking application...";
        await _authService.LockAsync();
        IsBusy = false;
    }

    private void OnAuthenticated(object? s, EventArgs e)
    {
        IsAuthenticated = true;
        Username = _authService.CurrentUsername ?? string.Empty;
        StatusMessage = "Unlocked";
    }

    private void OnLocked(object? s, EventArgs e)
    {
        IsAuthenticated = false;
        StatusMessage = "Locked";

        MasterPassword = string.Empty;
        ConfirmMasterPassword = string.Empty;
    }

    private async Task<bool> HandleLoginAsync()
    {
        ValidateAllProperties();
        if (string.IsNullOrEmpty(MasterPassword))
        {
            StatusMessage = "Master password cannot be empty";
            return false;
        }

        if (IsCreatingNewVault)
        {
            if (string.IsNullOrEmpty(ConfirmMasterPassword))
            {
                StatusMessage = "Confirm password cannot be empty";
                return false;
            }

            if (MasterPassword != ConfirmMasterPassword)
            {
                StatusMessage = "Passwords do not match";
                return false;
            }
        }

        IsBusy = true;
        StatusMessage = "Checking database...";
        bool isInitialized = await _databaseService.IsDatabaseInitializedAsync();


        if (!isInitialized)
        {
            StatusMessage = "Initializing database...";
            await _databaseService.InitializeDatabaseAsync(MasterPassword);
            StatusMessage = "Database initialized and unlocked.";
            _authService.NotifyAuthenticated();

            IsBusy = false;
            return true;
        }
        StatusMessage = "Validating password...";
        bool valid = await _authService.ValidateMasterPasswordAsync(MasterPassword);

        if (!valid)
        {
            StatusMessage = "Invalid master password.";
            IsBusy = false;
            return false;
        }

        StatusMessage = "Unlocked.";
        IsBusy = false;
        return true;
    }
}
