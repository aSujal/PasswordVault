using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShadUI.Dialogs;

namespace PasswordVault.ViewModels;

public partial class PasswordListViewModel(DialogManager dialogManager, AddPasswordDialogViewModel addPasswordVíewModel) : ViewModelBase
{
    [RelayCommand]
    public void ShowAddPasswordDialogCommand()
    {
        dialogManager.CreateDialog(addPasswordVíewModel)
            .WithMinWidth(500)
            .WithSuccessCallback(() =>
            {
            })
            .WithCancelCallback(() =>
            {
            })
            .Dismissible()
            .Show();
    }
}
