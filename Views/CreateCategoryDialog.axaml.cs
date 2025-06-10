using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace PasswordVault;

public partial class CreateCategoryDialog : UserControl
{
    public CreateCategoryDialog()
    {
        InitializeComponent();
    }
    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}