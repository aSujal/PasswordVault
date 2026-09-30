using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace PasswordVault.Views;

public partial class CategorySuggestionsDialog : UserControl
{
    public CategorySuggestionsDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
