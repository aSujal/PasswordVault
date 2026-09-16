using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LiteDB;
using PasswordVault.Models;

namespace PasswordVault.Services.Database;

public interface IDocumentFolderService
{
    event EventHandler? FoldersChanged;

    Task<List<DocumentFolder>> GetAllFoldersAsync();
    Task<DocumentFolder> AddFolderAsync(DocumentFolder folder);
    Task UpdateFolderAsync(DocumentFolder folder);
    Task DeleteFolderAsync(Guid id, bool deleteContents);
}

public class DocumentFolderService(DatabaseService databaseService) : IDocumentFolderService
{
    private const string CollectionName = "documentFolders";
    private const string DocumentsCollectionName = "documents";

    private readonly DatabaseService _databaseService = databaseService;

    public event EventHandler? FoldersChanged;

    public async Task<List<DocumentFolder>> GetAllFoldersAsync()
    {
        return await Task.Run(() =>
        {
            var db = _databaseService.OpenDatabase();
            var collection = db.GetCollection<DocumentFolder>(CollectionName);
            return collection.Query().OrderBy(f => f.Name).ToList();
        });
    }

    public async Task<DocumentFolder> AddFolderAsync(DocumentFolder folder)
    {
        return await Task.Run(() =>
        {
            var db = _databaseService.OpenDatabase();
            var collection = db.GetCollection<DocumentFolder>(CollectionName);

            folder.Id = Guid.NewGuid();
            folder.CreatedAt = DateTime.UtcNow;
            folder.UpdatedAt = DateTime.UtcNow;

            collection.Insert(folder);
            FoldersChanged?.Invoke(this, EventArgs.Empty);
            return folder;
        });
    }

    public async Task UpdateFolderAsync(DocumentFolder folder)
    {
        await Task.Run(() =>
        {
            var db = _databaseService.OpenDatabase();
            var collection = db.GetCollection<DocumentFolder>(CollectionName);

            if (folder.ParentId.HasValue && WouldCreateCycle(collection, folder.Id, folder.ParentId.Value))
                throw new InvalidOperationException("Cannot move a folder into its own subfolder.");

            folder.UpdatedAt = DateTime.UtcNow;
            collection.Update(folder);
            FoldersChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    public async Task DeleteFolderAsync(Guid id, bool deleteContents)
    {
        await Task.Run(() =>
        {
            var db = _databaseService.OpenDatabase();
            var folders = db.GetCollection<DocumentFolder>(CollectionName);
            var documents = db.GetCollection<VaultDocument>(DocumentsCollectionName);

            var folder = folders.FindById(id);
            if (folder == null) return;

            var children = folders.Query().Where(f => f.ParentId == id).ToList();
            var ownDocuments = documents.Query().Where(d => d.FolderId == id).ToList();

            if (deleteContents)
            {
                foreach (var child in children)
                    DeleteFolderInternal(db, child.Id, deleteContents: true);

                foreach (var document in ownDocuments)
                {
                    db.FileStorage.Delete(document.StorageId);
                    documents.Delete(document.Id);
                }
            }
            else
            {
                foreach (var child in children)
                {
                    child.ParentId = folder.ParentId;
                    child.UpdatedAt = DateTime.UtcNow;
                    folders.Update(child);
                }

                foreach (var document in ownDocuments)
                {
                    document.FolderId = folder.ParentId;
                    document.UpdatedAt = DateTime.UtcNow;
                    documents.Update(document);
                }
            }

            folders.Delete(id);
            FoldersChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    private void DeleteFolderInternal(ILiteDatabase db, Guid id, bool deleteContents)
    {
        var folders = db.GetCollection<DocumentFolder>(CollectionName);
        var documents = db.GetCollection<VaultDocument>(DocumentsCollectionName);

        foreach (var child in folders.Query().Where(f => f.ParentId == id).ToList())
            DeleteFolderInternal(db, child.Id, deleteContents);

        foreach (var document in documents.Query().Where(d => d.FolderId == id).ToList())
        {
            db.FileStorage.Delete(document.StorageId);
            documents.Delete(document.Id);
        }

        folders.Delete(id);
    }

    // Walks up from newParentId looking for folderId - if found, applying the move would turn
    // the tree into a cycle.
    private static bool WouldCreateCycle(ILiteCollection<DocumentFolder> collection, Guid folderId, Guid newParentId)
    {
        var current = collection.FindById(newParentId);
        while (current != null)
        {
            if (current.Id == folderId) return true;
            current = current.ParentId.HasValue ? collection.FindById(current.ParentId.Value) : null;
        }
        return false;
    }
}
