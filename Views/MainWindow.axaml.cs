using Avalonia;
using Avalonia.Markup.Xaml;
using PasswordVault.ViewModels;
using ShadUI;

namespace PasswordVault.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
#if DEBUG
        this.AttachDevTools();
#endif
    }
}