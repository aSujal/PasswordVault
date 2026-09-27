using Avalonia;
using Avalonia.Markup.Xaml;
using PasswordVault.Services;
using PasswordVault.ViewModels;
using ShadUI;

namespace PasswordVault.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        UpdateGlassClass();
        ActualThemeVariantChanged += (_, _) => UpdateGlassClass();
    }

    // Styles/Glass.axaml keys off this class for what theme colors alone can't change.
    private void UpdateGlassClass() => Classes.Set("Glass", ActualThemeVariant == ThemeService.Glass);

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
#if DEBUG
        this.AttachDevTools();
#endif
    }
}