using System;
using System.ComponentModel.DataAnnotations;

namespace PasswordVault.Validators;

public class UrlValidationAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null or "")
            return ValidationResult.Success;

        var url = value.ToString();

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
            || (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
        {
            return new ValidationResult("Please enter a valid URL (e.g., https://example.com)");
        }

        return ValidationResult.Success;
    }
}