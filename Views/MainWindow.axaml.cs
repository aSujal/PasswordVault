using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using PasswordVault.ViewModels;

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
    }

    private void LoginButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            var masterPasswordBox = this.FindControl<TextBox>("MasterPasswordBox");
            var confirmPasswordBox = this.FindControl<TextBox>("ConfirmMasterPasswordBox");

            if (masterPasswordBox != null && confirmPasswordBox != null)
            {
                string masterPwd = masterPasswordBox.Text ?? string.Empty;
                string confirmPwd = confirmPasswordBox.Text ?? string.Empty;
                if (vm.LoginCommand.CanExecute(new string[] { masterPwd, confirmPwd }))
                {
                    vm.LoginCommand.Execute(new string[] { masterPwd, confirmPwd });
                }
            }
        }
    }
}