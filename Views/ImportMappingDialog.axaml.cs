using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using PasswordVault.ViewModels;

namespace PasswordVault.Views;

public partial class ImportMappingDialog : UserControl
{
    public ImportMappingDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
