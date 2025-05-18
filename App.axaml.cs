using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using PasswordVault.Services.Auth;
using PasswordVault.Services.Crypto;
using PasswordVault.Services.Database;
using PasswordVault.Services.Sync;
using PasswordVault.ViewModels;
using PasswordVault.Views;
using System;
using System.Linq;

namespace PasswordVault;

public partial class App : Application
{
    private IServiceProvider _serviceProvider = null!;
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        ConfigureServices();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();

            desktop.MainWindow = new MainWindow
            {
                DataContext = _serviceProvider.GetRequiredService<MainViewModel>()
            };
            // desktop.Exit += OnExit;
        }

        base.OnFrameworkInitializationCompleted();
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

    private void ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<ICryptoService>(_ =>
            new CryptoService(new byte[32]));

        services.AddSingleton<DatabaseService>();
        services.AddSingleton<SyncService>();
        services.AddSingleton<AuthService>();

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<PasswordListViewModel>();
        services.AddSingleton<CategoryViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<SyncViewModel>();

        _serviceProvider = services.BuildServiceProvider(validateScopes: true);
    }
}