using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace PasswordVault;

public partial class AddCategoryDialog : UserControl
{
    public AddCategoryDialog()
    {
        InitializeComponent();
    }
    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void SelectIcon(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is string iconClass)
        {
            var viewModel = DataContext as ViewModels.AddCategoryDialogViewModel;
            viewModel?.SelectIcon(iconClass);
        }
    }
}