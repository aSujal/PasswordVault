using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordVault.Helper;
using PasswordVault.Models;
using PasswordVault.Services;
using PasswordVault.Services.Auth;
using PasswordVault.Services.Crypto;
using PasswordVault.Services.Database;
using PasswordVault.Services.Sync;
using ShadUI.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PasswordVault.ViewModels;

public partial class AddPasswordDialogViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _url = string.Empty;

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    private bool _isFavorite = false;

    [ObservableProperty]
    private ObservableCollection<Category> _categories = new();

    [ObservableProperty]
    private Category? _selectedCategory;

    [ObservableProperty]
    private int _passwordStrength = 0;

    [ObservableProperty]
    private string _passwordStrengthText = "Very Weak";

    [ObservableProperty]
    private string _passwordStrengthColor = "Red";

    private readonly DialogManager _dialogManager;
    private readonly CategoryService _categoryService;
    private readonly PasswordService _passwordService;
    private readonly ICryptoService _cryptoService;
    private readonly PasswordGenerator _passwordGenerator;
    private readonly CreateCategoryViewModel _createCategoryViewModel;
    private readonly AuthService _authService;
    public AddPasswordDialogViewModel(
        DialogManager dialogManager,
        CategoryService categoryService,
        ICryptoService cryptoService,
        PasswordService passwordService,
        DatabaseService databaseService,
        PasswordGenerator passwordGenerator,
        AuthService authService)
    {
        _dialogManager = dialogManager;
        _categoryService = categoryService;
        _cryptoService = cryptoService;
        _passwordService = passwordService;
        _passwordGenerator = passwordGenerator;
        _authService = authService;
        _createCategoryViewModel = new CreateCategoryViewModel(dialogManager);
        _authService.Authenticated += onAuthenticated;

        EvaluatePasswordStrength();
        _authService = authService;
    }

    private void onAuthenticated(object? sender, EventArgs e)
    {
        LoadCategories();
    }

    private async void LoadCategories()
    {

        var categories = await _categoryService.GetAllCategoriesAsync();
        Categories = new ObservableCollection<Category>(categories);

        // Set default category if available
        SelectedCategory = Categories.FirstOrDefault(c => c.Name == "Uncategorized") ?? Categories.FirstOrDefault();
    }

    private void EvaluatePasswordStrength()
    {
        var result = PasswordGenerator.EvaluatePasswordStrength(Password);
        PasswordStrength = result.Score;
        PasswordStrengthText = result.Level;
        PasswordStrengthColor = result.Level switch
        {
            "Very Weak" => "Red",
            "Weak" => "DarkOrange",
            "Moderate" => "Orange",
            "Strong" => "LightGreen",
            "Very Strong" => "Green", 
            _ => "White"
        };
    }

    partial void OnPasswordChanged(string value)
    {
        EvaluatePasswordStrength();
    }

    [RelayCommand]
    private void GeneratePassword()
    {
        Password = _passwordGenerator.GeneratePassword(
            length: 16,
            includeUppercase: true,
            includeLowercase: true,
            includeNumbers: true,
            includeSpecialChars: true);
    }

    [RelayCommand]
    private async Task CreateCategory()
    {
        //await _dialogManager.CreateDialog(_createCategoryViewModel)
        //    .WithMinWidth(400)
        //    .WithSuccessCallback(async () =>
        //    {
        //        // Create the new category
        //        var newCategory = new Category
        //        {
        //            Name = _createCategoryViewModel.Name,
        //            Color = _createCategoryViewModel.SelectedColor,
        //            Icon = _createCategoryViewModel.SelectedIcon
        //        };

        //        await _categoryService.AddCategoryAsync(newCategory);

        //        await Task.Delay(100);
        //        LoadCategories();

        //        SelectedCategory = Categories.FirstOrDefault(c => c.Id == newCategory.Id)
        //                          ?? throw new InvalidOperationException("New category not found in the list.");
        //    })
        //    .Dismissible
    }

    [RelayCommand]
    private async Task Submit()
    {
        try
        {
            var password = new Password
            {
                Title = Title,
                Username = Username,
                EncryptedPassword = _cryptoService.EncryptPassword(Password),
                Url = Url,
                Notes = Notes,
                Category = SelectedCategory?.Name ?? "Uncategorized",
                IsFavorite = IsFavorite
            };

            await _passwordService.AddPasswordAsync(password);

            _dialogManager.Close(this, new CloseDialogOptions { Success = true });
        }
        catch (Exception ex)
        {
            // Show error notification
            //await _dialogManager.("Failed to save password", ex.Message);
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _dialogManager.Close(this);
    }
}
