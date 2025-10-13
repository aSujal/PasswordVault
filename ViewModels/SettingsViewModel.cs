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

    public SettingsViewModel(ThemeWatcher watcher)
    {
        _watcher = watcher;

        CurrentColors = watcher.ThemeColors;

        _watcher.ThemeChanged += (_, colors) => CurrentColors = colors;

        SetLightCommand = new RelayCommand(() => _watcher.SwitchTheme(ThemeMode.Light));
        SetDarkCommand = new RelayCommand(() => _watcher.SwitchTheme(ThemeMode.Dark));
        SetSystemCommand = new RelayCommand(() => _watcher.SwitchTheme(ThemeMode.System));
    }
}
