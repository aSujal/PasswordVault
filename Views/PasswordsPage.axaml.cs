using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using HotAvalonia;
using System;

namespace PasswordVault.Views;

public partial class PasswordsPage : UserControl
{
    public PasswordsPage()
    {
        InitializeComponent();
        Initialize();
    }
    [AvaloniaHotReload]
    private void Initialize()
    {
        Console.WriteLine("PasswordsPage reloaded at " + DateTime.Now);
        // Re-initialize or refresh logic here
    }
    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}