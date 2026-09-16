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

public partial class AddFolderDialogViewModel : ViewModelBase
{
    private readonly DialogManager _dialogManager;
    private readonly IDocumentFolderService _folderService;

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _selectedColor = "#4285F4";
    [ObservableProperty] private ObservableCollection<DocumentFolder> _availableParents = [];
    [ObservableProperty] private DocumentFolder? _selectedParent;
    [ObservableProperty] private bool _isEditMode;
    [ObservableProperty] private string _dialogTitle = "New Folder";
    [ObservableProperty] private string _submitButtonText = "Create";

    private DocumentFolder? _folderToEdit;

    public DocumentFolder? CreatedFolder { get; private set; }

    public AddFolderDialogViewModel(DialogManager dialogManager, IDocumentFolderService folderService)
    {
        _dialogManager = dialogManager;
        _folderService = folderService;
    }

    public async Task InitializeAsync(Guid? initialParentId)
    {
        IsEditMode = false;
        _folderToEdit = null;
        Name = string.Empty;
        SelectedColor = "#4285F4";
        DialogTitle = "New Folder";
        SubmitButtonText = "Create";
        CreatedFolder = null;
        ClearAllErrors();

        await LoadParentOptionsAsync(excludeId: null);
        SelectedParent = AvailableParents.FirstOrDefault(f => f.Id == initialParentId);
    }

    public async Task InitializeForEditAsync(DocumentFolder folder)
    {
        IsEditMode = true;
        _folderToEdit = folder;
        Name = folder.Name;
        SelectedColor = folder.Color;
        DialogTitle = "Edit Folder";
        SubmitButtonText = "Save";
        ClearAllErrors();

        // A folder can't become its own parent (or descendant - DocumentFolderService rejects
        // that at save time), so it's excluded from the picker rather than offered and refused.
        await LoadParentOptionsAsync(excludeId: folder.Id);
        SelectedParent = AvailableParents.FirstOrDefault(f => f.Id == folder.ParentId);
    }

    private async Task LoadParentOptionsAsync(Guid? excludeId)
    {
        var all = await _folderService.GetAllFoldersAsync();
        AvailableParents = new ObservableCollection<DocumentFolder>(
            all.Where(f => f.Id != excludeId));
    }

    [RelayCommand]
    private async Task Submit()
    {
        ClearAllErrors();
        if (string.IsNullOrWhiteSpace(Name))
            AddError(nameof(Name), "Folder name is required.");

        if (HasErrors) return;

        try
        {
            if (IsEditMode && _folderToEdit != null)
            {
                _folderToEdit.Name = Name.Trim();
                _folderToEdit.Color = SelectedColor;
                _folderToEdit.ParentId = SelectedParent?.Id;
                await _folderService.UpdateFolderAsync(_folderToEdit);
            }
            else
            {
                CreatedFolder = await _folderService.AddFolderAsync(new DocumentFolder
                {
                    Name = Name.Trim(),
                    Color = SelectedColor,
                    ParentId = SelectedParent?.Id,
                });
            }

            _dialogManager.Close(this, new CloseDialogOptions { Success = true });
        }
        catch (Exception ex)
        {
            AddError(nameof(Name), ex.Message);
        }
    }

    [RelayCommand]
    private void Cancel() => _dialogManager.Close(this);
}
