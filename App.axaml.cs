using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using PasswordVault.Extensions;
using PasswordVault.Helper;
using PasswordVault.Services;
using PasswordVault.Services.Auth;
using PasswordVault.Services.Crypto;
using PasswordVault.Services.Database;
using PasswordVault.Services.Sync;
using PasswordVault.ViewModels;
using PasswordVault.Views;
using ShadUI;

namespace PasswordVault;

public partial class App : Application
{
    private IServiceProvider _serviceProvider = null!;
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        ConfigureServices();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var themeWatcher = _serviceProvider.GetRequiredService<ThemeWatcher>();
            themeWatcher.Initialize();

            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            //DisableAvaloniaDataAnnotationValidation();
            var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
            _ = mainViewModel.InitializeAsync();

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };
            // desktop.Exit += OnExit;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ConfigureServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICryptoService>(_ =>
            new CryptoService(new byte[32]));
        services.AddSingleton<ThemeWatcher>(_ => new ThemeWatcher(Application.Current!));
        services.AddSingleton<ShadUI.ToastManager>();

        services.AddSingleton<DatabaseService>();
        services.AddSingleton<SyncService>();
        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddSingleton<PasswordGenerator>();
        services.AddSingleton<ICategoryService, CategoryService>();

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<PasswordListViewModel>();
        services.AddSingleton<AddCategoryDialogViewModel>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<AddPasswordDialogViewModel>();
        services.AddSingleton<CategoryViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<SyncViewModel>();
        services.AddSingleton<DialogManager>();

        services.AddSingleton<MainWindow>();
        services.AddSingleton<PasswordsPage>();
        services.AddSingleton<AddPasswordDialog>();
        services.AddSingleton<DashboardPage>();
        services.AddSingleton<SettingsPage>();
        services.AddSingleton<SyncPage>();

        _serviceProvider = services.BuildServiceProvider(validateScopes: true)
                                    .RegisterDialogs();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}