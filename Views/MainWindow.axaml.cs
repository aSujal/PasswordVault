using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using PasswordVault.ViewModels;

namespace PasswordVault.Views;

public partial class MainWindow : ShadUI.Controls.Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        this.AttachDevTools();
    }
}