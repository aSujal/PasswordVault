using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordVault.Services.ImportExport;
using ShadUI;

namespace PasswordVault.ViewModels;

public partial class ImportMappingViewModel(DialogManager dialogManager) : ViewModelBase
{
    private readonly DialogManager _dialogManager = dialogManager ?? throw new ArgumentNullException(nameof(dialogManager));

    [ObservableProperty] private CsvPreview _preview = new();
    [ObservableProperty] private ObservableCollection<string> _availableHeaders = new();

    [ObservableProperty] private string? _titleHeader;
    [ObservableProperty] private string? _usernameHeader;
    [ObservableProperty] private string? _passwordHeader;
    [ObservableProperty] private string? _urlHeader;
    [ObservableProperty] private string? _notesHeader;
    [ObservableProperty] private string? _categoryHeader;
    [ObservableProperty] private string? _tagsHeader;
    [ObservableProperty] private string? _isFavoriteHeader;

    public void Initialize(CsvPreview preview, ImportMapping suggestion)
    {
        Preview = preview;
        AvailableHeaders = new ObservableCollection<string>(preview.Headers);

        TitleHeader = suggestion.TitleHeader;
        UsernameHeader = suggestion.UsernameHeader;
        PasswordHeader = suggestion.PasswordHeader;
        UrlHeader = suggestion.UrlHeader;
        NotesHeader = suggestion.NotesHeader;
        CategoryHeader = suggestion.CategoryHeader;
        TagsHeader = suggestion.TagsHeader;
        IsFavoriteHeader = suggestion.IsFavoriteHeader;
    }

    public ImportMapping GetMapping() => new()
    {
        TitleHeader = TitleHeader,
        UsernameHeader = UsernameHeader,
        PasswordHeader = PasswordHeader,
        UrlHeader = UrlHeader,
        NotesHeader = NotesHeader,
        CategoryHeader = CategoryHeader,
        TagsHeader = TagsHeader,
        IsFavoriteHeader = IsFavoriteHeader
    };

    [RelayCommand]
    private void Submit()
    {
        //TODO Make sure its a valid mapping
        if (string.IsNullOrWhiteSpace(TitleHeader))
        {
            return;
        }
        _dialogManager.Close(this, new CloseDialogOptions { Success = true });
    }

    [RelayCommand]
    private void Cancel()
    {
        _dialogManager.Close(this);
    }
}
