using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShadUI.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PasswordVault.ViewModels;

public partial class CreateCategoryViewModel : ViewModelBase
{
    private readonly DialogManager _dialogManager;
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _selectedColor = "#00A638";

    [ObservableProperty]
    private string _selectedIcon = string.Empty;
    public ICommand CreateCommand { get; }

    public CreateCategoryViewModel(DialogManager dialogManager)
    {
        _dialogManager = dialogManager;
        CreateCommand = new RelayCommand(Create);
    }
    private void Create()
    {
        _dialogManager.Close(this, new CloseDialogOptions { Success = true });
    }
}
