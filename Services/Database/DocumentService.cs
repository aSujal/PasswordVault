using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LiteDB;
using PasswordVault.Models;

namespace PasswordVault.Services.Database;

// Filters for listing documents. Folders/Favorites/Attached/Trash all live in the sidebar as
// mutually exclusive views, so one query shape covers the whole documents page.
public record DocumentQuery
{
    public Guid? FolderId { get; init; }
    public bool FavoritesOnly { get; init; }
    public bool AttachedOnly { get; init; }
    public bool Trash { get; init; }
    public string? Search { get; init; }
}

public interface IDocumentService
{
    event EventHandler? DocumentsChanged;

    Task<List<VaultDocument>> GetDocumentsAsync(DocumentQuery query);
    Task<List<VaultDocument>> GetDocumentsForPasswordAsync(Guid passwordId);
    Task<VaultDocument?> GetDocumentAsync(Guid id);

    Task<VaultDocument> AddDocumentAsync(VaultDocument document, Stream content);
    Task<VaultDocument> AddDocumentFromFileAsync(string filePath, Guid? folderId, Guid? passwordId);

    // Returns a stream over the document's bytes. The stream is bound to the shared vault
    // database - callers must dispose it promptly and never hold it open across a vault lock
    // or master-password change.
    Task<Stream> OpenDocumentAsync(Guid id);

    Task UpdateDocumentAsync(VaultDocument document);
    Task MoveDocumentsAsync(IEnumerable<Guid> ids, Guid? targetFolderId);
    Task SoftDeleteDocumentAsync(Guid id);
    Task RestoreDocumentAsync(Guid id);
    Task PermanentlyDeleteDocumentAsync(Guid id);
    Task<int> DeleteDocumentsForPasswordAsync(Guid passwordId);
}

public class DocumentService(DatabaseService databaseService) : IDocumentService
{
    public const long MaxDocumentSizeBytes = 50L * 1024 * 1024;

    private const string CollectionName = "documents";

    private readonly DatabaseService _databaseService = databaseService;

    public event EventHandler? DocumentsChanged;

    public async Task<List<VaultDocument>> GetDocumentsAsync(DocumentQuery query)
    {
        return await Task.Run(() =>
        {
            var db = _databaseService.OpenDatabase();
            var collection = db.GetCollection<VaultDocument>(CollectionName);

            IEnumerable<VaultDocument> results = collection.Query()
                .Where(d => d.IsDeleted == query.Trash)
                .ToList();

            if (!query.Trash)
            {
                if (query.FavoritesOnly)
                    results = results.Where(d => d.IsFavorite);
                else if (query.AttachedOnly)
                    results = results.Where(d => d.PasswordId != null);
                else if (query.FolderId.HasValue)
                    results = results.Where(d => d.FolderId == query.FolderId);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var term = query.Search.Trim();
                results = results.Where(d =>
                    d.FileName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (d.Notes != null && d.Notes.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                    d.Tags.Any(t => t.Contains(term, StringComparison.OrdinalIgnoreCase)));
            }

            return results.OrderByDescending(d => d.AddedAt).ToList();
        });
    }

    public async Task<List<VaultDocument>> GetDocumentsForPasswordAsync(Guid passwordId)
    {
        return await Task.Run(() =>
        {
            var db = _databaseService.OpenDatabase();
            var collection = db.GetCollection<VaultDocument>(CollectionName);
            return collection.Query()
                .Where(d => d.PasswordId == passwordId && !d.IsDeleted)
                .ToList();
        });
    }

    public async Task<VaultDocument?> GetDocumentAsync(Guid id)
    {
        return await Task.Run(() =>
        {
            var db = _databaseService.OpenDatabase();
            var collection = db.GetCollection<VaultDocument>(CollectionName);
            return collection.FindById(id);
        });
    }

    public async Task<VaultDocument> AddDocumentAsync(VaultDocument document, Stream content)
    {
        return await Task.Run(() =>
        {
            var db = _databaseService.OpenDatabase();
            var collection = db.GetCollection<VaultDocument>(CollectionName);

            document.Id = Guid.NewGuid();
            document.StorageId = $"doc/{document.Id}";
            document.AddedAt = DateTime.UtcNow;
            document.UpdatedAt = DateTime.UtcNow;

            db.FileStorage.Upload(document.StorageId, document.FileName, content);
            collection.Insert(document);

            DocumentsChanged?.Invoke(this, EventArgs.Empty);
            return document;
        });
    }

    public async Task<VaultDocument> AddDocumentFromFileAsync(string filePath, Guid? folderId, Guid? passwordId)
    {
        var info = new FileInfo(filePath);
        if (!info.Exists)
            throw new FileNotFoundException("File not found.", filePath);
        if (info.Length > MaxDocumentSizeBytes)
            throw new InvalidOperationException($"'{info.Name}' is {info.Length / (1024 * 1024)} MB, which is over the {MaxDocumentSizeBytes / (1024 * 1024)} MB limit for a single document.");

        using var stream = info.OpenRead();
        var document = new VaultDocument
        {
            FolderId = folderId,
            PasswordId = passwordId,
            FileName = info.Name,
            ContentType = GuessContentType(info.Extension),
            SizeBytes = info.Length,
        };
        return await AddDocumentAsync(document, stream);
    }

    public async Task<Stream> OpenDocumentAsync(Guid id)
    {
        var document = await GetDocumentAsync(id) ?? throw new InvalidOperationException("Document not found.");
        var db = _databaseService.OpenDatabase();
        return db.FileStorage.OpenRead(document.StorageId);
    }

    public async Task UpdateDocumentAsync(VaultDocument document)
    {
        await Task.Run(() =>
        {
            var db = _databaseService.OpenDatabase();
            var collection = db.GetCollection<VaultDocument>(CollectionName);

            var existing = collection.FindById(document.Id)
                ?? throw new InvalidOperationException("Document not found.");

            document.StorageId = existing.StorageId;
            document.SizeBytes = existing.SizeBytes;
            document.AddedAt = existing.AddedAt;
            document.UpdatedAt = DateTime.UtcNow;

            collection.Update(document);
            DocumentsChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    public async Task MoveDocumentsAsync(IEnumerable<Guid> ids, Guid? targetFolderId)
    {
        await Task.Run(() =>
        {
            var db = _databaseService.OpenDatabase();
            var collection = db.GetCollection<VaultDocument>(CollectionName);

            foreach (var id in ids)
            {
                var document = collection.FindById(id);
                if (document == null) continue;

                document.FolderId = targetFolderId;
                document.UpdatedAt = DateTime.UtcNow;
                collection.Update(document);
            }

            DocumentsChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    public async Task SoftDeleteDocumentAsync(Guid id)
    {
        await Task.Run(() =>
        {
            var db = _databaseService.OpenDatabase();
            var collection = db.GetCollection<VaultDocument>(CollectionName);

            var document = collection.FindById(id);
            if (document == null) return;

            document.IsDeleted = true;
            document.DeletedAt = DateTime.UtcNow;
            document.UpdatedAt = DateTime.UtcNow;
            collection.Update(document);

            DocumentsChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    public async Task RestoreDocumentAsync(Guid id)
    {
        await Task.Run(() =>
        {
            var db = _databaseService.OpenDatabase();
            var collection = db.GetCollection<VaultDocument>(CollectionName);

            var document = collection.FindById(id);
            if (document == null) return;

            document.IsDeleted = false;
            document.DeletedAt = null;
            document.UpdatedAt = DateTime.UtcNow;
            collection.Update(document);

            DocumentsChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    public async Task PermanentlyDeleteDocumentAsync(Guid id)
    {
        await Task.Run(() =>
        {
            var db = _databaseService.OpenDatabase();
            var collection = db.GetCollection<VaultDocument>(CollectionName);

            var document = collection.FindById(id);
            if (document == null) return;

            db.FileStorage.Delete(document.StorageId);
            collection.Delete(id);

            DocumentsChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    public async Task<int> DeleteDocumentsForPasswordAsync(Guid passwordId)
    {
        return await Task.Run(() =>
        {
            var db = _databaseService.OpenDatabase();
            var collection = db.GetCollection<VaultDocument>(CollectionName);

            var toDelete = collection.Query().Where(d => d.PasswordId == passwordId).ToList();
            foreach (var document in toDelete)
            {
                db.FileStorage.Delete(document.StorageId);
                collection.Delete(document.Id);
            }

            if (toDelete.Count > 0)
                DocumentsChanged?.Invoke(this, EventArgs.Empty);

            return toDelete.Count;
        });
    }

    private static string GuessContentType(string extension) => extension.ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".bmp" => "image/bmp",
        ".txt" => "text/plain",
        ".md" => "text/markdown",
        ".json" => "application/json",
        ".csv" => "text/csv",
        ".xml" => "application/xml",
        ".doc" or ".docx" => "application/msword",
        ".xls" or ".xlsx" => "application/vnd.ms-excel",
        ".zip" => "application/zip",
        _ => "application/octet-stream",
    };
}
