using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using NSubstitute;
using PasswordVault.Helper;
using PasswordVault.Models;
using PasswordVault.Services.Auth;
using PasswordVault.Services.Crypto;
using PasswordVault.Services.Database;
using PasswordVault.ViewModels;
using Xunit;

namespace PasswordVault.Tests.ViewModels;

public class AddPasswordDialogViewModelTests
{
    private readonly ICategoryService _categoryService;
    private readonly ICryptoService _cryptoService;
    private readonly IPasswordService _passwordService;
    private readonly PasswordGenerator _passwordGenerator;
    private readonly IAuthService _authService;

    public AddPasswordDialogViewModelTests()
    {
        // Mock only interfaces - concrete classes cannot be mocked with NSubstitute
        _categoryService = Substitute.For<ICategoryService>();
        _cryptoService = Substitute.For<ICryptoService>();
        _passwordService = Substitute.For<IPasswordService>();
        _authService = Substitute.For<IAuthService>();

        // PasswordGenerator is concrete but pure logic - instantiate a real one
        _passwordGenerator = new PasswordGenerator();
    }

    private AddPasswordDialogViewModel CreateViewModel()
    {
        // Pass null for dependencies we can't mock (DialogManager, ToastManager, etc.)
        // These are external UI components from ShadUI
        return new AddPasswordDialogViewModel(
            null!, // DialogManager (can't mock - external concrete class)
            null!, // ToastManager (can't mock)
            _categoryService,
            null!, // AddCategoryDialogViewModel (can't mock)
            _cryptoService,
            _passwordService,
            null!, // DatabaseService (not needed for these tests)
            _passwordGenerator,
            _authService
        );
    }

    [Fact]
    public void EvaluatePasswordStrength_UpdatesScoreAndText()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        vm.Password = "WeakPass"; // 8 chars, mixed case

        // Assert
        Assert.NotEqual(0, vm.PasswordStrength);
        Assert.NotEqual("Very Weak", vm.PasswordStrengthText);
    }

    [Fact]
    public void GeneratePassword_SetsPasswordProperty()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        vm.GeneratePasswordCommand.Execute(null);

        // Assert
        Assert.False(string.IsNullOrEmpty(vm.Password));
        Assert.Equal(16, vm.Password.Length);
    }

    [Fact]
    public void PasswordStrength_VeryWeak_ForEmptyPassword()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        vm.Password = "";

        // Assert
        Assert.Equal(0, vm.PasswordStrength);
        Assert.Equal("Very Weak", vm.PasswordStrengthText);
    }

    [Fact]
    public void PasswordStrength_Strong_ForComplexPassword()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        vm.Password = "MyStr0ng!Pass@123"; // 17 chars, upper, lower, digit, special

        // Assert
        Assert.True(vm.PasswordStrength >= 70);
        Assert.Contains(vm.PasswordStrengthText, new[] { "Strong", "Very Strong" });
    }
}
