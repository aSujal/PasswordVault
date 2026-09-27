using System;
using System.Collections.Generic;
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
        SelectedParent = AvailableParents.FirstOrDefault(f => f.Id == initialParentId) ?? RootOption;
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

        // A folder can't move into itself or its own subtree (DocumentFolderService rejects that
        // at save time), so those are left out of the picker rather than offered and refused.
        await LoadParentOptionsAsync(excludeId: folder.Id);
        SelectedParent = AvailableParents.FirstOrDefault(f => f.Id == folder.ParentId) ?? RootOption;
    }

    // Stands in for "no parent": the ComboBox can't be cleared once something is picked, so
    // without this a nested folder could never be moved back to the top level.
    private static readonly DocumentFolder RootOption = new() { Id = Guid.Empty, Name = "Root", Color = "#9E9E9E" };

    private static Guid? ParentIdOf(DocumentFolder? parent) => parent == RootOption ? null : parent?.Id;

    private async Task LoadParentOptionsAsync(Guid? excludeId)
    {
        var all = await _folderService.GetAllFoldersAsync();
        var excluded = new HashSet<Guid>();
        if (excludeId.HasValue)
        {
            excluded.Add(excludeId.Value);
            // Folders come back ordered by name, not depth, so sweep until no new descendant turns up.
            int before;
            do
            {
                before = excluded.Count;
                foreach (var f in all.Where(f => f.ParentId.HasValue && excluded.Contains(f.ParentId.Value)))
                    excluded.Add(f.Id);
            } while (excluded.Count != before);
        }

        AvailableParents = new ObservableCollection<DocumentFolder>(
            all.Where(f => !excluded.Contains(f.Id)).Prepend(RootOption));
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
                _folderToEdit.ParentId = ParentIdOf(SelectedParent);
                await _folderService.UpdateFolderAsync(_folderToEdit);
            }
            else
            {
                CreatedFolder = await _folderService.AddFolderAsync(new DocumentFolder
                {
                    Name = Name.Trim(),
                    Color = SelectedColor,
                    ParentId = ParentIdOf(SelectedParent),
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
