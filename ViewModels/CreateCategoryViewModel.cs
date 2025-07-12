using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordVault.Services.Database;
using ShadUI.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PasswordVault.ViewModels;

public partial class CreateCategoryViewModel : ViewModelBase
{
    private readonly DialogManager _dialogManager;
    private readonly CategoryService _categoryService;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _selectedColor = "#FF00A638";

    [ObservableProperty]
    private string _selectedIcon = string.Empty;

    public ICommand CreateCommand { get; }

    public ObservableCollection<string> AvailableIcons { get; } = [
        "fa-solid fa-key",
        "fa-solid fa-lock",
        "fa-solid fa-user",
        "fa-solid fa-envelope",
        "fa-solid fa-globe",
        "fa-solid fa-credit-card",
        "fa-solid fa-building",
        "fa-solid fa-shopping-cart",
        "fa-solid fa-heart",
        "fa-solid fa-shield",
        "fa-solid fa-wifi",
        "fa-solid fa-laptop",
        "fa-solid fa-cloud",
        "fa-solid fa-mobile",
        "fa-solid fa-code",
        "fa-solid fa-database",
        "fa-solid fa-gamepad",
        "fa-solid fa-book",
        "fa-solid fa-camera",
        "fa-solid fa-user-shield",
        "fa-solid fa-id-card",
        "fa-solid fa-passport",
        "fa-solid fa-sim-card",
        "fa-solid fa-server",
        "fa-solid fa-user-secret",
        "fa-solid fa-file-invoice",
        "fa-solid fa-clipboard-list",
        "fa-solid fa-vault"
     ];

    public CreateCategoryViewModel(DialogManager dialogManager, CategoryService categoryService)
    {
        _dialogManager = dialogManager;
        _categoryService = categoryService;
        CreateCommand = new RelayCommand(Create);
    }

    private async void Create()
    {
        ClearErrors(nameof(Name));
        ClearErrors(nameof(SelectedColor));
        ClearErrors(nameof(SelectedIcon));
        if (string.IsNullOrWhiteSpace(Name))
        {
            AddError(nameof(Name), "Category name is required.");
            return;
        }

        if( Name.Length < 3 || Name.Length > 50)
        {
            AddError(nameof(Name), "Category name must be between 3 and 50 characters.");
            return;
        }

        if(SelectedColor == null || SelectedColor.Length != 7 || !SelectedColor.StartsWith("#"))
        {
            AddError(nameof(SelectedColor), "Invalid color format. Use a hex color code (e.g., #00A638).");
            return;
        }

        if(SelectedIcon == null || SelectedIcon.Length == 0)
        {
            AddError(nameof(SelectedIcon), "Please select an icon for the category.");
            return;
        }

        if (await _categoryService.CategoryExistsAsync(Name))
        {
            AddError(nameof(Name), "A category with this name already exists.");
            return;
        }

        var category = new Models.Category
        {
            Name = Name.Trim(),
            Color = SelectedColor,
            Icon = SelectedIcon,
        };

        await _categoryService.AddCategoryAsync(category);
        _dialogManager.Close(this, new CloseDialogOptions { Success = true });
    }

    [RelayCommand]
    private void SelectColor(string? color)
    {
        if (!string.IsNullOrEmpty(color))
        {
            SelectedColor = color;
        }
    }

    [RelayCommand]
    public void SelectIcon(string? icon)
    {
        if (!string.IsNullOrEmpty(icon))
            SelectedIcon = icon;
    }
}
