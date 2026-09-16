using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordVault.Models;
using PasswordVault.Services.Auth;
using PasswordVault.Services.Database;
using ShadUI;

namespace PasswordVault.ViewModels;

public partial class DocumentListViewModel : ViewModelBase
{
    private readonly DialogManager _dialogManager;
    private readonly IDocumentService _documentService;
    private readonly IDocumentFolderService _folderService;
    private readonly EditDocumentDialogViewModel _editDocumentDialogViewModel;
    private readonly AddFolderDialogViewModel _addFolderDialogViewModel;
    private readonly DocumentPreviewViewModel _previewViewModel;
    private readonly IAuthService _authService;
    public readonly ToastManager _toastManager;

    private List<DocumentFolder> _allFolders = [];

    [ObservableProperty] private ObservableCollection<DocumentFolderNode> _folderNodes = [];
    [ObservableProperty] private DocumentFolderNode? _selectedNode;
    [ObservableProperty] private string _breadcrumb = "All Documents";

    [ObservableProperty] private ObservableCollection<VaultDocument> _documents = [];
    [ObservableProperty] private string? _searchText;
    [ObservableProperty] private bool _isSearching;
    [ObservableProperty] private long _totalSizeBytes;

    [ObservableProperty] private bool _isSelectionMode;
    [ObservableProperty] private int _selectedCount;
    [ObservableProperty] private bool _isAllSelected;

    [ObservableProperty] private ObservableCollection<DocumentFolder> _bulkMoveOptions = [];
    [ObservableProperty] private DocumentFolder? _bulkMoveTarget;

    public DocumentListViewModel(
        DialogManager dialogManager,
        IDocumentService documentService,
        IDocumentFolderService folderService,
        EditDocumentDialogViewModel addDocumentDialogViewModel,
        AddFolderDialogViewModel addFolderDialogViewModel,
        DocumentPreviewViewModel previewViewModel,
        IAuthService authService,
        ToastManager toastManager)
    {
        _dialogManager = dialogManager;
        _documentService = documentService;
        _folderService = folderService;
        _editDocumentDialogViewModel = addDocumentDialogViewModel;
        _addFolderDialogViewModel = addFolderDialogViewModel;
        _previewViewModel = previewViewModel;
        _authService = authService;
        _toastManager = toastManager;

        _authService.Authenticated += (_, _) => _ = LoadAllAsync();
        _documentService.DocumentsChanged += (_, _) => _ = ApplyQueryAsync();
        _folderService.FoldersChanged += (_, _) => _ = LoadAllAsync();
    }

    private async Task LoadAllAsync()
    {
        _allFolders = await _folderService.GetAllFoldersAsync();
        await BuildFolderNodesAsync();
        await ApplyQueryAsync();
    }

    private async Task BuildFolderNodesAsync()
    {
        var all = await _documentService.GetDocumentsAsync(new DocumentQuery());
        var trash = await _documentService.GetDocumentsAsync(new DocumentQuery { Trash = true });
        var countsByFolder = all
            .Where(d => d.FolderId.HasValue)
            .GroupBy(d => d.FolderId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var nodes = new List<DocumentFolderNode>
        {
            new() { PseudoKind = DocumentPseudoKind.All, DocumentCount = all.Count },
            new() { PseudoKind = DocumentPseudoKind.Favorites, DocumentCount = all.Count(d => d.IsFavorite) },
            new() { PseudoKind = DocumentPseudoKind.Attached, DocumentCount = all.Count(d => d.PasswordId != null) },
            new() { PseudoKind = DocumentPseudoKind.Trash, DocumentCount = trash.Count },
        };

        AppendFolderChildren(nodes, parentId: null, depth: 0, countsByFolder);

        var previouslySelectedFolderId = SelectedNode?.FolderId;
        FolderNodes = new ObservableCollection<DocumentFolderNode>(nodes);
        SelectedNode = previouslySelectedFolderId.HasValue
            ? FolderNodes.FirstOrDefault(n => n.FolderId == previouslySelectedFolderId) ?? FolderNodes[0]
            : FolderNodes.FirstOrDefault(n => n.PseudoKind == (SelectedNode?.PseudoKind ?? DocumentPseudoKind.All)) ?? FolderNodes[0];

        BulkMoveOptions = new ObservableCollection<DocumentFolder>(_allFolders);
    }

    private void AppendFolderChildren(List<DocumentFolderNode> nodes, Guid? parentId, int depth, Dictionary<Guid, int> countsByFolder)
    {
        foreach (var folder in _allFolders.Where(f => f.ParentId == parentId).OrderBy(f => f.Name))
        {
            nodes.Add(new DocumentFolderNode
            {
                Folder = folder,
                Depth = depth,
                DocumentCount = countsByFolder.GetValueOrDefault(folder.Id),
            });
            AppendFolderChildren(nodes, folder.Id, depth + 1, countsByFolder);
        }
    }

    private DocumentQuery BuildQuery() => SelectedNode?.PseudoKind switch
    {
        DocumentPseudoKind.Favorites => new DocumentQuery { FavoritesOnly = true, Search = SearchText },
        DocumentPseudoKind.Attached => new DocumentQuery { AttachedOnly = true, Search = SearchText },
        DocumentPseudoKind.Trash => new DocumentQuery { Trash = true, Search = SearchText },
        _ => new DocumentQuery { FolderId = SelectedNode?.FolderId, Search = SearchText },
    };

    private async Task ApplyQueryAsync()
    {
        IsSearching = true;
        try
        {
            var results = await _documentService.GetDocumentsAsync(BuildQuery());
            Documents = new ObservableCollection<VaultDocument>(results);
            TotalSizeBytes = results.Sum(d => d.SizeBytes);
            Breadcrumb = BuildBreadcrumb();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading documents: {ex.Message}");
            Documents = [];
        }
        finally
        {
            IsSearching = false;
        }
    }

    private string BuildBreadcrumb()
    {
        if (SelectedNode == null || SelectedNode.IsPseudoNode) return SelectedNode?.Name ?? "All Documents";

        var segments = new List<string>();
        var current = SelectedNode.Folder;
        while (current != null)
        {
            segments.Insert(0, current.Name);
            current = current.ParentId.HasValue ? _allFolders.FirstOrDefault(f => f.Id == current.ParentId) : null;
        }
        return string.Join(" › ", segments);
    }

    public async Task ExecuteSearchAsync() => await ApplyQueryAsync();

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAllAsync();

    [RelayCommand]
    private async Task SelectNode(DocumentFolderNode? node)
    {
        if (node == null || SelectedNode == node) return;
        SelectedNode = node;
        await ApplyQueryAsync();
    }
    //todo
    public async Task AddDocumentAsync(string filePath, Guid? folderId)
    {
        try
        {
            await _documentService.AddDocumentFromFileAsync(filePath, folderId, null);
            await LoadAllAsync();
            _toastManager.CreateToast("Document Added").WithContent($"{Path.GetFileName(filePath)} added successfully.").ShowSuccess();
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Add Failed").WithContent($"Failed to add {Path.GetFileName(filePath)}: {ex.Message}").ShowError();
        }
    }

    public async Task AddFilesAsync(IReadOnlyList<string> filePaths)
    {
        var folderId = SelectedNode?.FolderId;
        int added = 0;
        foreach (var path in filePaths)
        {
            try
            {
                await _documentService.AddDocumentFromFileAsync(path, folderId, null);
                added++;
            }
            catch (Exception ex)
            {
                _toastManager.CreateToast("Import Failed").WithContent($"{Path.GetFileName(path)}: {ex.Message}").ShowError();
            }
        }

        if (added > 0)
        {
            await LoadAllAsync();
            _toastManager.CreateToast("Documents Added").WithContent($"{added} document(s) imported.").ShowSuccess();
        }
    }

    [RelayCommand]
    private void AddFolder()
    {
        _ = _addFolderDialogViewModel.InitializeAsync(SelectedNode?.FolderId);
        _dialogManager.CreateDialog(_addFolderDialogViewModel)
            .WithMinWidth(400)
            .WithSuccessCallback(async () => await LoadAllAsync())
            .Dismissible()
            .Show();
    }

    [RelayCommand]
    private void EditFolder(DocumentFolderNode? node)
    {
        if (node?.Folder == null) return;
        _ = _addFolderDialogViewModel.InitializeForEditAsync(node.Folder);
        _dialogManager.CreateDialog(_addFolderDialogViewModel)
            .WithMinWidth(400)
            .WithSuccessCallback(async () => await LoadAllAsync())
            .Dismissible()
            .Show();
    }

    [RelayCommand]
    private void ConfirmDeleteFolder(DocumentFolderNode? node)
    {
        if (node?.Folder == null) return;

        _dialogManager.CreateDialog("Delete Folder", $"Delete '{node.Folder.Name}'? Its subfolders and documents move up to the parent folder - nothing is deleted.")
            .WithPrimaryButton("Delete", async () =>
            {
                await _folderService.DeleteFolderAsync(node.Folder.Id, deleteContents: false);
                await LoadAllAsync();
            }, DialogButtonStyle.Destructive)
            .WithCancelButton("Cancel")
            .Dismissible()
            .Show();
    }

    [RelayCommand]
    private void PreviewDocument(VaultDocument? document)
    {
        if (document == null) return;
        _ = _previewViewModel.InitializeAsync(document);
        _dialogManager.CreateDialog(_previewViewModel).WithMinWidth(600).Dismissible().Show();
    }

    [RelayCommand]
    private void EditDocument(VaultDocument? document)
    {
        if (document == null) return;
        _ = _editDocumentDialogViewModel.InitializeForEditAsync(document);
        _dialogManager.CreateDialog(_editDocumentDialogViewModel)
            .WithMinWidth(500)
            .WithSuccessCallback(async () => await LoadAllAsync())
            .Dismissible()
            .Show();
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync(VaultDocument? document)
    {
        if (document == null) return;
        document.IsFavorite = !document.IsFavorite;
        try
        {
            await _documentService.UpdateDocumentAsync(document);
        }
        catch (Exception ex)
        {
            document.IsFavorite = !document.IsFavorite;
            Console.WriteLine($"Error updating favorite status: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ConfirmDeleteDocument(VaultDocument? document)
    {
        if (document == null) return;
        _dialogManager.CreateDialog("Move to Trash", $"Move '{document.FileName}' to trash?")
            .WithPrimaryButton("Delete", async () =>
            {
                await _documentService.SoftDeleteDocumentAsync(document.Id);
                await LoadAllAsync();
            }, DialogButtonStyle.Destructive)
            .WithCancelButton("Cancel")
            .Dismissible()
            .Show();
    }

    [RelayCommand]
    private async Task RestoreDocumentAsync(VaultDocument? document)
    {
        if (document == null) return;
        await _documentService.RestoreDocumentAsync(document.Id);
        await LoadAllAsync();
        _toastManager.CreateToast("Restored").WithContent($"'{document.FileName}' restored.").ShowSuccess();
    }

    [RelayCommand]
    private void ConfirmPermanentlyDelete(VaultDocument? document)
    {
        if (document == null) return;
        _dialogManager.CreateDialog("Delete Permanently", $"Permanently delete '{document.FileName}'? This cannot be undone.")
            .WithPrimaryButton("Delete Forever", async () =>
            {
                await _documentService.PermanentlyDeleteDocumentAsync(document.Id);
                await LoadAllAsync();
            }, DialogButtonStyle.Destructive)
            .WithCancelButton("Cancel")
            .Dismissible()
            .Show();
    }

    [RelayCommand]
    private void ToggleSelectionMode()
    {
        IsSelectionMode = !IsSelectionMode;
        if (!IsSelectionMode) ClearSelections();
    }

    [RelayCommand]
    private void SelectionChanged() => UpdateSelectedCount();

    [RelayCommand]
    private void SelectAll()
    {
        IsAllSelected = !IsAllSelected;
        foreach (var document in Documents) document.IsSelected = IsAllSelected;
        UpdateSelectedCount();
    }

    private void UpdateSelectedCount()
    {
        SelectedCount = Documents.Count(d => d.IsSelected);
        IsAllSelected = Documents.Count > 0 && SelectedCount == Documents.Count;
    }

    private void ClearSelections()
    {
        foreach (var document in Documents) document.IsSelected = false;
        SelectedCount = 0;
        IsAllSelected = false;
    }

    [RelayCommand]
    private async Task ApplyBulkMoveAsync()
    {
        var targetIds = Documents.Where(d => d.IsSelected).Select(d => d.Id).ToList();
        if (targetIds.Count == 0) return;

        await _documentService.MoveDocumentsAsync(targetIds, BulkMoveTarget?.Id);
        IsSelectionMode = false;
        ClearSelections();
        BulkMoveTarget = null;
        await LoadAllAsync();

        _toastManager.CreateToast("Moved").WithContent($"{targetIds.Count} document(s) moved.").ShowSuccess();
    }

    [RelayCommand]
    private void ConfirmDeleteSelected()
    {
        var selected = Documents.Where(d => d.IsSelected).ToList();
        if (selected.Count == 0) return;

        var trashing = SelectedNode?.PseudoKind != DocumentPseudoKind.Trash;
        var verb = trashing ? "move" : "permanently delete";

        _dialogManager.CreateDialog("Confirm Deletion", $"Are you sure you want to {verb} {selected.Count} document(s)?")
            .WithPrimaryButton(trashing ? "Delete" : "Delete Forever", async () =>
            {
                foreach (var document in selected)
                {
                    if (trashing) await _documentService.SoftDeleteDocumentAsync(document.Id);
                    else await _documentService.PermanentlyDeleteDocumentAsync(document.Id);
                }

                IsSelectionMode = false;
                ClearSelections();
                await LoadAllAsync();

                _toastManager.CreateToast("Deleted").WithContent($"{selected.Count} document(s) removed.").ShowSuccess();
            }, DialogButtonStyle.Destructive)
            .WithCancelButton("Cancel")
            .Dismissible()
            .Show();
    }
}
