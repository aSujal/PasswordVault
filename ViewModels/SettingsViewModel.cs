using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShadUI;

namespace PasswordVault.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly ThemeWatcher _watcher;

    // exposes the whole color set to the view
    [ObservableProperty] private ThemeColors _currentColors;

    public IRelayCommand SetLightCommand { get; }
    public IRelayCommand SetDarkCommand { get; }
    public IRelayCommand SetSystemCommand { get; }
    public IRelayCommand ManageCategoriesCommand { get; }

    private readonly DialogManager _dialogManager;
    private readonly ManageCategoriesViewModel _manageCategoriesViewModel;

    public SettingsViewModel(ThemeWatcher watcher, DialogManager dialogManager, ManageCategoriesViewModel manageCategoriesViewModel)
    {
        _watcher = watcher;
        _dialogManager = dialogManager;
        _manageCategoriesViewModel = manageCategoriesViewModel;

        CurrentColors = watcher.ThemeColors;

        _watcher.ThemeChanged += (_, colors) => CurrentColors = colors;

        SetLightCommand = new RelayCommand(() => _watcher.SwitchTheme(ThemeMode.Light));
        SetDarkCommand = new RelayCommand(() => _watcher.SwitchTheme(ThemeMode.Dark));
        SetSystemCommand = new RelayCommand(() => _watcher.SwitchTheme(ThemeMode.System));

        ManageCategoriesCommand = new RelayCommand(OpenManageCategories);
    }

    private void OpenManageCategories()
    {
        _dialogManager.CreateDialog(_manageCategoriesViewModel)
            .WithMinWidth(500)
            .WithSuccessCallback(async () => await _manageCategoriesViewModel.InitializeAsync())
            .Dismissible()
            .Show();
    }
}
