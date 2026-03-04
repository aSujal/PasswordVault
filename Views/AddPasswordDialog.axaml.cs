using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace PasswordVault.Views;

public partial class AddPasswordDialog : UserControl
{
    public AddPasswordDialog()
    {
        InitializeComponent();
    }
    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}