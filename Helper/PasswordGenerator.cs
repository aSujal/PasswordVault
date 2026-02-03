using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace PasswordVault.Helper;


public class PasswordStrengthResult
{
    public int Score { get; set; }
    public string Level { get; set; } = string.Empty;
    public List<string> Suggestions { get; set; } = [];
    public bool IsCommonPassword { get; set; }
}
public class PasswordGenerator
{
    private const string UppercaseChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string LowercaseChars = "abcdefghijklmnopqrstuvwxyz";
    private const string NumberChars = "0123456789";
    private const string SpecialChars = "!@#$%^&*()_+-=[]{}|;:,.<>?";
    private const string SimilarChars = "il1Lo0O";

    public static readonly string[] CommonPasswords = [
        "password", "123456", "password123", "admin", "qwerty", "letmein", "welcome",
        "monkey", "1234567890", "abc123", "111111", "dragon", "master", "princess",
        "login", "solo", "qwertyuiop", "starwars", "654321", "sunshine"
    ];

    private static readonly string[] WordList = [
        "apple", "banana", "cherry", "dragon", "eagle", "forest", "guitar", "harbor",
        "island", "jungle", "knight", "lemon", "mountain", "ninja", "ocean", "palace",
        "queen", "river", "sunset", "tiger", "umbrella", "violet", "wizard", "xenon",
        "yellow", "zebra", "anchor", "bridge", "castle", "dolphin", "elephant", "falcon",
        "galaxy", "hammer", "igloo", "jacket", "kangaroo", "lighthouse", "magnet", "notebook"
    ];

    public PasswordGenerator()
    {
        // No initialization needed - using CSPRNG directly
    }

    public string GeneratePassword(int length = 16, bool includeUppercase = true, bool includeLowercase = true,
          bool includeNumbers = true, bool includeSpecialChars = true, bool excludeSimilar = false)
    {
        if (length < 4)
            throw new ArgumentException("Password length must be at least 4 characters");

        if (!includeUppercase && !includeLowercase && !includeNumbers && !includeSpecialChars)
            throw new ArgumentException("At least one character type must be included");

        var characterSet = new StringBuilder();
        if (includeUppercase) characterSet.Append(excludeSimilar ? new string(UppercaseChars.Where(c => !SimilarChars.Contains(c)).ToArray()) : UppercaseChars);
        if (includeLowercase) characterSet.Append(excludeSimilar ? new string(LowercaseChars.Where(c => !SimilarChars.Contains(c)).ToArray()) : LowercaseChars);
        if (includeNumbers) characterSet.Append(excludeSimilar ? new string(NumberChars.Where(c => !SimilarChars.Contains(c)).ToArray()) : NumberChars);
        if (includeSpecialChars) characterSet.Append(SpecialChars);

        var allChars = characterSet.ToString().ToCharArray();
        var passwordChars = new char[length];
        for (int i = 0; i < length; i++)
        {
            passwordChars[i] = allChars[RandomNumberGenerator.GetInt32(allChars.Length)];
        }

        return new string(passwordChars);
    }

    public string GenerateMemorablePassword(int wordCount = 4, string separator = "-", bool includeNumbers = true)
    {
        if (wordCount < 2)
            throw new ArgumentException("Word count must be at least 2");

        var words = new List<string>();
        for (int i = 0; i < wordCount; i++)
        {
            var word = WordList[RandomNumberGenerator.GetInt32(WordList.Length)];

            // Capitalize first letter randomly
            if (RandomNumberGenerator.GetInt32(2) == 0)
            {
                word = char.ToUpper(word[0]) + word.Substring(1);
            }

            words.Add(word);
        }

        var password = string.Join(separator, words);

        if (includeNumbers)
        {
            // Add 2-3 random numbers
            var numberCount = RandomNumberGenerator.GetInt32(2, 4);
            for (int i = 0; i < numberCount; i++)
            {
                password += RandomNumberGenerator.GetInt32(0, 10).ToString();
            }
        }

        return password;
    }

    public static PasswordStrengthResult EvaluatePasswordStrength(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return new PasswordStrengthResult
            {
                Score = 0,
                Level = "Very Weak",
                Suggestions = new List<string> { "Password is required" }
            };
        }

        var score = 0;
        var suggestions = new List<string>();
        var hasUpper = password.Any(char.IsUpper);
        var hasLower = password.Any(char.IsLower);
        var hasDigit = password.Any(char.IsDigit);
        var hasSpecial = password.Any(c => SpecialChars.Contains(c));
        var length = password.Length;
        var isCommon = CommonPasswords.Contains(password.ToLower());

        if (length >= 12) score += 25;
        else if (length >= 8) score += 15;
        else if (length >= 6) score += 10;
        else suggestions.Add("Use at least 8 characters");

        if (hasUpper) score += 15; else suggestions.Add("Include uppercase letters");
        if (hasLower) score += 15; else suggestions.Add("Include lowercase letters");
        if (hasDigit) score += 15; else suggestions.Add("Include numbers");
        if (hasSpecial) score += 15; else suggestions.Add("Include special characters");

        if (isCommon)
        {
            score -= 30;
            suggestions.Add("Avoid common passwords");
        }

        if (length >= 16) score += 15;

        string level = score switch
        {
            >= 90 => "Very Strong",
            >= 70 => "Strong",
            >= 50 => "Moderate",
            >= 30 => "Weak",
            _ => "Very Weak"
        };

        return new PasswordStrengthResult
        {
            Score = Math.Max(0, Math.Min(100, score)),
            Level = level,
            Suggestions = suggestions,
            IsCommonPassword = isCommon
        };
    }
}

