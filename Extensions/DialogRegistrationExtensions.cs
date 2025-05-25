using Microsoft.Extensions.DependencyInjection;
using PasswordVault.ViewModels;
using PasswordVault.Views;
using ShadUI.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PasswordVault.Extensions;

public static class DialogRegistrationExtensions
{
    public static IServiceProvider RegisterDialogs(this IServiceProvider service)
    {
        var dialogService = service.GetRequiredService<DialogManager>();

        // Register dialogs here
        dialogService.Register<MainWindow, MainViewModel>();
        dialogService.Register<PasswordsPage, PasswordListViewModel>();
        dialogService.Register<AddPasswordDialog, AddPasswordDialogViewModel>();

        return service;
    }

}
