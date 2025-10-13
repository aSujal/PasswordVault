using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Windows.UI.Xaml.Controls;

namespace PasswordVault;

public partial class AddPasswordDialog : Avalonia.Controls.UserControl
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