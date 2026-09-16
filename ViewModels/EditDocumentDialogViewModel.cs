using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordVault.Models;
using PasswordVault.Services.Database;
using ShadUI;

namespace PasswordVault.ViewModels;

// Editing an already-imported document's metadata: rename, move, tag, note, favorite. Adding a
// document has no dialog of its own - picked files are uploaded immediately by
// DocumentListViewModel.AddFilesAsync, so this only ever runs in edit mode.
public partial class EditDocumentDialogViewModel : ViewModelBase
{
    private readonly DialogManager _dialogManager;
    private readonly IDocumentService _documentService;
    private readonly IDocumentFolderService _folderService;

    [ObservableProperty] private string _fileName = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;
    [ObservableProperty] private string _tagsText = string.Empty;
    [ObservableProperty] private bool _isFavorite;
    [ObservableProperty] private ObservableCollection<DocumentFolder> _folders = [];
    [ObservableProperty] private DocumentFolder? _selectedFolder;

    private VaultDocument? _documentToEdit;

    public event EventHandler? DocumentUpdated;

    public EditDocumentDialogViewModel(DialogManager dialogManager, IDocumentService documentService, IDocumentFolderService folderService)
    {
        _dialogManager = dialogManager;
        _documentService = documentService;
        _folderService = folderService;
    }

    public async Task InitializeForEditAsync(VaultDocument document)
    {
        _documentToEdit = document;
        FileName = document.FileName;
        Notes = document.Notes ?? string.Empty;
        TagsText = string.Join(", ", document.Tags);
        IsFavorite = document.IsFavorite;
        ClearAllErrors();

        var allFolders = await _folderService.GetAllFoldersAsync();
        Folders = new ObservableCollection<DocumentFolder>(allFolders);
        SelectedFolder = Folders.FirstOrDefault(f => f.Id == document.FolderId);
    }

    [RelayCommand]
    private async Task Submit()
    {
        ClearAllErrors();
        if (string.IsNullOrWhiteSpace(FileName))
            AddError(nameof(FileName), "File name is required.");

        if (HasErrors || _documentToEdit == null) return;

        try
        {
            _documentToEdit.FileName = FileName.Trim();
            _documentToEdit.Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim();
            _documentToEdit.Tags = [.. TagsText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
            _documentToEdit.IsFavorite = IsFavorite;
            _documentToEdit.FolderId = SelectedFolder?.Id;

            await _documentService.UpdateDocumentAsync(_documentToEdit);
            DocumentUpdated?.Invoke(this, EventArgs.Empty);
            _dialogManager.Close(this, new CloseDialogOptions { Success = true });
        }
        catch (Exception ex)
        {
            AddError(nameof(FileName), ex.Message);
        }
    }

    [RelayCommand]
    private void Cancel() => _dialogManager.Close(this);
}
