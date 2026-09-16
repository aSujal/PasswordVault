using System;
using PasswordVault.Models;

namespace PasswordVault.ViewModels;

// One row in the folders sidebar. Real folders are shown flattened with indentation by Depth
// rather than as a collapsible tree - keeps the sidebar to a single ItemsControl instead of a
// recursive template, which is plenty for the handful of folders a personal vault will have.
// PseudoKind selects one of the fixed views (All/Favorites/Attached/Trash) instead of a folder.
public enum DocumentPseudoKind
{
    None,
    All,
    Favorites,
    Attached,
    Trash,
}

public class DocumentFolderNode
{
    public DocumentFolder? Folder { get; init; }
    public DocumentPseudoKind PseudoKind { get; init; } = DocumentPseudoKind.None;
    public int Depth { get; init; }
    public int DocumentCount { get; set; }

    public bool IsPseudoNode => PseudoKind != DocumentPseudoKind.None;

    public Guid? FolderId => Folder?.Id;

    public string Name => Folder?.Name ?? PseudoKind switch
    {
        DocumentPseudoKind.All => "All Documents",
        DocumentPseudoKind.Favorites => "Favorites",
        DocumentPseudoKind.Attached => "Attached to Entries",
        DocumentPseudoKind.Trash => "Trash",
        _ => string.Empty,
    };

    public string Icon => Folder?.Icon ?? PseudoKind switch
    {
        DocumentPseudoKind.All => "fa-solid fa-folder-open",
        DocumentPseudoKind.Favorites => "fa-solid fa-star",
        DocumentPseudoKind.Attached => "fa-solid fa-link",
        DocumentPseudoKind.Trash => "fa-solid fa-trash",
        _ => "fa-solid fa-folder",
    };

    public string Color => Folder?.Color ?? "#9E9E9E";
}
