using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NSubstitute;
using Xunit;

namespace PasswordVault.Tests;

/// <summary>
/// 📚 REFERENCE GUIDE: How to write tests in xUnit
/// Use this file as a cheat sheet when writing your own tests!
/// </summary>
public class HelpTest
{
    // ═══════════════════════════════════════════════════════════════════
    // 🔹 BASIC ASSERTIONS
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Example_Equality()
    {
        // Assert.Equal checks if two values are the same
        int result = 24 + 123;
        Assert.Equal(147, result);

        string name = "Password";
        Assert.Equal("Password", name);
    }

    [Fact]
    public void Example_NotEqual()
    {
        string password = "secret123";
        Assert.NotEqual("wrongpassword", password);
    }

    [Fact]
    public void Example_TrueAndFalse()
    {
        bool isValid = true;
        Assert.True(isValid);

        bool isEmpty = false;
        Assert.False(isEmpty);
    }

    [Fact]
    public void Example_NullChecks()
    {
        string? nullString = null;
        Assert.Null(nullString);

        string notNullString = "hello";
        Assert.NotNull(notNullString);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 🔹 STRING ASSERTIONS
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Example_StringContains()
    {
        string message = "Password is too weak";
        Assert.Contains("weak", message);
        Assert.DoesNotContain("strong", message);
    }

    [Fact]
    public void Example_StringStartsEndsWith()
    {
        string url = "https://example.com/api";
        Assert.StartsWith("https://", url);
        Assert.EndsWith("/api", url);
    }

    [Fact]
    public void Example_StringEmpty()
    {
        string empty = "";
        Assert.Empty(empty);

        string notEmpty = "hello";
        Assert.NotEmpty(notEmpty);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 🔹 COLLECTION ASSERTIONS
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Example_CollectionContains()
    {
        var passwords = new List<string> { "pass1", "pass2", "pass3" };

        Assert.Contains("pass2", passwords);
        Assert.DoesNotContain("pass99", passwords);
    }

    [Fact]
    public void Example_CollectionCount()
    {
        var items = new List<int> { 1, 2, 3, 4, 5 };

        Assert.Equal(5, items.Count);
        Assert.NotEmpty(items);
    }

    [Fact]
    public void Example_AllItemsMatch()
    {
        var numbers = new List<int> { 2, 4, 6, 8 };

        // Assert.All checks that EVERY item matches a condition
        Assert.All(numbers, n => Assert.True(n % 2 == 0)); // All are even
    }

    // ═══════════════════════════════════════════════════════════════════
    // 🔹 NUMERIC ASSERTIONS
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Example_NumericRange()
    {
        int passwordStrength = 75;

        Assert.InRange(passwordStrength, 0, 100); // Between 0 and 100
        Assert.True(passwordStrength >= 70);      // At least 70
    }

    // ═══════════════════════════════════════════════════════════════════
    // 🔹 EXCEPTION TESTING
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Example_ThrowsException()
    {
        // Test that a specific exception is thrown
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await Task.Delay(1);
            throw new ArgumentNullException("password");
        });
    }

    [Fact]
    public async Task Example_ThrowsExceptionAsync()
    {
        // For async methods that throw
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await Task.Delay(1);
            throw new InvalidOperationException("Something went wrong");
        });
    }

    // ═══════════════════════════════════════════════════════════════════
    // 🔹 MOCKING WITH NSUBSTITUTE
    // ═══════════════════════════════════════════════════════════════════

    // First, define a simple interface for the example
    public interface ICalculator
    {
        int Add(int a, int b);
        Task<string> GetResultAsync();
    }

    [Fact]
    public void Example_MockingBasics()
    {
        // Create a mock/fake implementation
        var calculator = Substitute.For<ICalculator>();

        // Set up what it should return
        calculator.Add(2, 3).Returns(5);

        // Use the mock
        int result = calculator.Add(2, 3);

        // Verify the result
        Assert.Equal(5, result);
    }

    [Fact]
    public void Example_MockingAnyArgument()
    {
        var calculator = Substitute.For<ICalculator>();

        // Arg.Any<T>() matches any value of that type
        calculator.Add(Arg.Any<int>(), Arg.Any<int>()).Returns(100);

        Assert.Equal(100, calculator.Add(1, 2));
        Assert.Equal(100, calculator.Add(999, 888));
    }

    [Fact]
    public void Example_VerifyMethodWasCalled()
    {
        var calculator = Substitute.For<ICalculator>();

        // Use the mock
        calculator.Add(5, 10);

        // Verify that Add was called with specific arguments
        calculator.Received().Add(5, 10);

        // Verify it was NOT called with other arguments
        calculator.DidNotReceive().Add(1, 1);
    }

    [Fact]
    public async Task Example_MockingAsyncMethods()
    {
        var calculator = Substitute.For<ICalculator>();

        // Mock async methods
        calculator.GetResultAsync().Returns(Task.FromResult("Success"));

        string result = await calculator.GetResultAsync();

        Assert.Equal("Success", result);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 🔹 PARAMETERIZED TESTS (Theory)
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("", false)]           // Empty = invalid
    [InlineData("abc", false)]        // Too short = invalid
    [InlineData("password123!", true)] // Good = valid
    public void Example_ParameterizedTest(string password, bool expectedValid)
    {
        bool isValid = password.Length >= 8;
        Assert.Equal(expectedValid, isValid);
    }

    [Theory]
    [InlineData(1, 2, 3)]
    [InlineData(10, 20, 30)]
    [InlineData(-5, 5, 0)]
    public void Example_MultipleInputs(int a, int b, int expected)
    {
        Assert.Equal(expected, a + b);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 🔹 TEST STRUCTURE: Arrange-Act-Assert
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void Example_AAA_Pattern()
    {
        // ARRANGE - Set up test data and dependencies
        var passwords = new List<string>();
        string newPassword = "MySecurePass123!";

        // ACT - Execute the code being tested
        passwords.Add(newPassword);

        // ASSERT - Verify the expected outcome
        Assert.Single(passwords);
        Assert.Contains(newPassword, passwords);
    }
}
