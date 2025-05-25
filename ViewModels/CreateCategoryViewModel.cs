using CommunityToolkit.Mvvm.ComponentModel;
using ShadUI.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PasswordVault.ViewModels;

internal partial class CreateCategoryViewModel(DialogManager dialogManager) : ViewModelBase
{

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _selectedColor = string.Empty;

    [ObservableProperty]
    private string _selectedIcon = string.Empty;


}
