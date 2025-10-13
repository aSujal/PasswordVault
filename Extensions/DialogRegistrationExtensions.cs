using Microsoft.Extensions.DependencyInjection;
using PasswordVault.ViewModels;
using PasswordVault.Views;
using ShadUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;

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
        dialogService.Register<CreateCategoryDialog, CreateCategoryViewModel>();

        return service;
    }

}
