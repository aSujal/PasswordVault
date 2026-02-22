using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordVault.Models;
using PasswordVault.Services.Database;

namespace PasswordVault.ViewModels;

public partial class FilterPopupViewModel : ViewModelBase
{
    private readonly ICategoryService _categoryService;

    public event EventHandler? FiltersApplied;

    [ObservableProperty]
    private ObservableCollection<Category> _categories = [];

    [ObservableProperty]
    private Category? _selectedCategory;

    [ObservableProperty]
    private bool _showFavoritesOnly;

    [ObservableProperty]
    private string _selectedSortOption = "Newest First";

    [ObservableProperty]
    private bool _isAnyFilterActive;


    public ObservableCollection<string> SortOptions { get; } =
    [
        "Newest First",
        "Oldest First",
        "Title A-Z",
        "Title Z-A",
        "Last Used"
    ];

    public FilterPopupViewModel(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    public async Task LoadCategoriesAsync()
    {
        try
        {
            var allCategories = await _categoryService.GetAllCategoriesAsync();
            Categories = new ObservableCollection<Category>(allCategories);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading categories for filter: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ApplyFilters()
    {
        UpdateFilterActiveState();
        FiltersApplied?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void ClearFilters()
    {
        SelectedCategory = null;
        ShowFavoritesOnly = false;
        SelectedSortOption = "Newest First";
        UpdateFilterActiveState();
        FiltersApplied?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateFilterActiveState()
    {
        IsAnyFilterActive = SelectedCategory != null
                            || ShowFavoritesOnly
                            || SelectedSortOption != "Newest First";
    }
}
