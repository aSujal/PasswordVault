using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using PasswordVault.Models;
using ShadUI;

namespace PasswordVault.ViewModels;

public class CategorySuggestion(Password password, string category, bool isNew)
{
    public Password Password { get; } = password;
    public string Category { get; } = category;
    public bool IsNew { get; } = isNew;
    public string CurrentCategory => Password.Category?.Name ?? "Uncategorized";
    public bool IsSelected { get; set; } = true;
}

/// <summary>
/// Lets the user review AI category suggestions and untick any before they are applied.
/// </summary>
public partial class CategorySuggestionsViewModel(DialogManager dialogManager, List<CategorySuggestion> suggestions) : ViewModelBase
{
    public List<CategorySuggestion> Suggestions { get; } = suggestions;

    public string Summary
    {
        get
        {
            int newCount = Suggestions.Where(s => s.IsNew).Select(s => s.Category).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            return newCount == 0
                ? $"{Suggestions.Count} entries would move to a different category."
                : $"{Suggestions.Count} entries would move, creating {newCount} new {(newCount == 1 ? "category" : "categories")}.";
        }
    }

    [RelayCommand]
    private void Apply() => dialogManager.Close(this, new CloseDialogOptions { Success = true });

    [RelayCommand]
    private void Cancel() => dialogManager.Close(this);
}
