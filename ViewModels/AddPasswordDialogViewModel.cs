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
    // Properties for Edit Mode
    [ObservableProperty]
    private bool _isEditMode;

    private Password? _passwordToEdit;

    [ObservableProperty]
    private string _submitButtonText = "Add"; // Default to "Add"

    [ObservableProperty]
    private string _dialogTitle = "Add New Password";

    private readonly DialogManager _dialogManager;
    private readonly CategoryService _categoryService;
    private readonly PasswordService _passwordService;
    private readonly ICryptoService _cryptoService;
    private readonly PasswordGenerator _passwordGenerator;
    private readonly CreateCategoryViewModel _createCategoryViewModel;
    private readonly AuthService _authService;

    public event EventHandler? PasswordAddedSuccessfully;
    public event EventHandler? PasswordUpdatedSuccessfully;

    public AddPasswordDialogViewModel(
        DialogManager dialogManager,
        CategoryService categoryService,
        CreateCategoryViewModel createCategoryViewModel,
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
        _createCategoryViewModel = createCategoryViewModel;
        _authService.Authenticated += onAuthenticated;

        EvaluatePasswordStrength();
        _authService = authService;
    }

    public void ResetForAdd()
    {
        IsEditMode = false;
        _passwordToEdit = null;
        Title = string.Empty;
        Username = string.Empty;
        Password = string.Empty;
        Url = string.Empty;
        Notes = string.Empty;
        IsFavorite = false;
        SelectedCategory = Categories.FirstOrDefault(c => c.Name == "Uncategorized") ?? Categories.FirstOrDefault();
        SubmitButtonText = "Add";
        DialogTitle = "Add New Password";
        if (!Categories.Any())
        {
            LoadCategories();
        }
    }

    public async void SetPasswordToEdit(Password password)
    {
        _passwordToEdit = password;
        IsEditMode = true;
        SubmitButtonText = "Save Changes";
        DialogTitle = "Edit Password";

        // Ensure categories are loaded so we can match the category
        if (!Categories.Any())
        {
            await LoadCategoriesAsync(); // Make sure this is awaitable and loads categories
        }

        Title = password.Title;
        Username = password.Username;
        Password = _cryptoService.DecryptPassword(password.EncryptedPassword ?? string.Empty); // Decrypt password
        Url = password.Url;
        Notes = password.Notes;
        IsFavorite = password.IsFavorite;

        if (password.Category != null)
        {
            SelectedCategory = Categories.FirstOrDefault(c => c.Id == password.Category.Id);
        }
        else
        {
            SelectedCategory = Categories.FirstOrDefault(c => c.Name == "Uncategorized") ?? Categories.FirstOrDefault();
        }
    }

    private void onAuthenticated(object? sender, EventArgs e)
    {
        LoadCategories();
        ResetForAdd();
    }
    private void LoadCategories()
    {
        _ = LoadCategoriesAsync();
    }

    private async Task LoadCategoriesAsync()
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
    public void CreateCategory()
    {
        _dialogManager.CreateDialog(_createCategoryViewModel)
            .WithMinWidth(400)
            .WithSuccessCallback(async () =>
            {
                await LoadCategoriesAsync();
            })
            .WithCancelCallback(() =>
            {
            })
            .Dismissible()
            .Show();

    }

    [RelayCommand]
    private async Task Submit()
    {
        try
        {
            if (IsEditMode && _passwordToEdit != null)
            {
                _passwordToEdit.Title = Title;
                _passwordToEdit.Username = Username;
                _passwordToEdit.EncryptedPassword = _cryptoService.EncryptPassword(Password);
                _passwordToEdit.Url = Url;
                _passwordToEdit.Notes = Notes;
                _passwordToEdit.Category = SelectedCategory;
                _passwordToEdit.IsFavorite = IsFavorite;

                await _passwordService.UpdatePasswordAsync(_passwordToEdit);
                PasswordUpdatedSuccessfully?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                var newPassword = new Password
                {
                    Title = Title,
                    Username = Username,
                    EncryptedPassword = _cryptoService.EncryptPassword(Password),
                    Url = Url,
                    Notes = Notes,
                    Category = SelectedCategory,
                    IsFavorite = IsFavorite
                };
                await _passwordService.AddPasswordAsync(newPassword);
                PasswordAddedSuccessfully?.Invoke(this, EventArgs.Empty);
            }

            _dialogManager.Close(this, new CloseDialogOptions { Success = true });
        }
        catch (Exception ex)
        {
            // Show error notification
            //await _dialogManager.("Failed to save password", ex.Message);
        }
        finally
        {
            ResetForAdd();
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _dialogManager.Close(this);
    }
}
