using System;
using Microsoft.Extensions.DependencyInjection;
using PasswordVault.ViewModels;
using PasswordVault.Views;
using ShadUI;

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
        dialogService.Register<AddCategoryDialog, AddCategoryDialogViewModel>();
        dialogService.Register<ManageCategoriesDialog, ManageCategoriesViewModel>();
        dialogService.Register<ImportMappingDialog, ImportMappingViewModel>();

        return service;
    }

}
