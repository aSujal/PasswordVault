using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordVault.Models;
using PasswordVault.Services.Database;
using ShadUI;

namespace PasswordVault.ViewModels;

public partial class ManageCategoriesViewModel : ViewModelBase
{
    private readonly ICategoryService _categoryService;
    private readonly DialogManager _dialogManager;
    private readonly AddCategoryDialogViewModel _addCategoryViewModel;
    private readonly ToastManager _toastManager;

    [ObservableProperty]
    private ObservableCollection<Category> _categories = new();

    [ObservableProperty]
    private bool _isLoading;

    public ManageCategoriesViewModel(
        ICategoryService categoryService,
        DialogManager dialogManager,
        AddCategoryDialogViewModel addCategoryViewModel,
        ToastManager toastManager)
    {
        _categoryService = categoryService;
        _dialogManager = dialogManager;
        _addCategoryViewModel = addCategoryViewModel;
        _toastManager = toastManager;
    }

    public async Task InitializeAsync()
    {
        await LoadCategoriesAsync();
    }

    private async Task LoadCategoriesAsync()
    {
        IsLoading = true;
        try
        {
            var categories = await _categoryService.GetAllCategoriesAsync();
            Categories = new ObservableCollection<Category>(categories);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading categories: {ex.Message}");
            _toastManager.CreateToast("Error")
                .WithContent("Failed to load categories.")
                .ShowError();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void AddCategory()
    {
        _addCategoryViewModel.Initialize();
        ShowCategoryDialog("Add Category");
    }

    [RelayCommand]
    private void EditCategory(Category category)
    {
        _addCategoryViewModel.SetCategoryToEdit(category);
        ShowCategoryDialog("Edit Category");
    }

    private void ShowCategoryDialog(string title)
    {
        _dialogManager.CreateDialog(_addCategoryViewModel)
            .WithMinWidth(400)
            .WithSuccessCallback(async () =>
            {
                await LoadCategoriesAsync();
            })
            .Dismissible()
            .Show();
    }

    [RelayCommand]
    private async Task DeleteCategoryAsync(Category category)
    {
        if (category == null) return;

        // Prevent deleting special categories if needed, though service handles logic.
        // Assuming "Uncategorized" shouldn't be deleted or at least warned.

        _dialogManager.CreateDialog("Confirm Deletion",
            $"Are you sure you want to delete '{category.Name}'? Associated passwords will be moved to 'Uncategorized'.")
            .WithPrimaryButton("Delete", async () =>
            {
                try
                {
                    await _categoryService.DeleteCategoryAsync(category.Id);
                    await LoadCategoriesAsync();
                    _toastManager.CreateToast("Deleted")
                        .WithContent($"Category '{category.Name}' deleted.")
                        .ShowSuccess();
                }
                catch (Exception ex)
                {
                    _toastManager.CreateToast("Error")
                       .WithContent($"Failed to delete: {ex.Message}")
                       .ShowError();
                }
            }, DialogButtonStyle.Destructive)
            .WithCancelButton("Cancel")
            .Dismissible()
            .Show();
    }

    [RelayCommand]
    private void Close()
    {
        _dialogManager.Close(this);
    }
}
