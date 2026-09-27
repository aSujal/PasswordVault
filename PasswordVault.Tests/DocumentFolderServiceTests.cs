using System;
using System.IO;
using System.Threading.Tasks;
using PasswordVault.Models;
using PasswordVault.Services.Crypto;
using PasswordVault.Services.Database;
using Xunit;

namespace PasswordVault.Tests.Services;

public class DocumentFolderServiceTests : IDisposable
{
    private readonly string _tempFolder;
    private readonly DatabaseService _db;
    private readonly DocumentService _documentService;
    private readonly DocumentFolderService _folderService;

    public DocumentFolderServiceTests()
    {
        _tempFolder = Path.Combine(Path.GetTempPath(), "PasswordVaultTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempFolder);

        var cryptoService = new CryptoService(new byte[32]);
        _db = new DatabaseService(cryptoService, _tempFolder);
        _documentService = new DocumentService(_db);
        _folderService = new DocumentFolderService(_db);

        _db.InitializeDatabaseAsync("master-password").GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempFolder, recursive: true); } catch { /* best effort cleanup */ }
    }

    [Fact]
    public async Task AddFolderAsync_NestedUnderParent_RoundTrips()
    {
        var parent = await _folderService.AddFolderAsync(new DocumentFolder { Name = "Personal" });
        var child = await _folderService.AddFolderAsync(new DocumentFolder { Name = "IDs", ParentId = parent.Id });

        var all = await _folderService.GetAllFoldersAsync();

        Assert.Contains(all, f => f.Id == parent.Id && f.ParentId == null);
        Assert.Contains(all, f => f.Id == child.Id && f.ParentId == parent.Id);
    }

    [Fact]
    public async Task UpdateFolderAsync_MovingIntoOwnDescendant_Throws()
    {
        var parent = await _folderService.AddFolderAsync(new DocumentFolder { Name = "Parent" });
        var child = await _folderService.AddFolderAsync(new DocumentFolder { Name = "Child", ParentId = parent.Id });

        parent.ParentId = child.Id;

        await Assert.ThrowsAsync<InvalidOperationException>(() => _folderService.UpdateFolderAsync(parent));
    }

    [Fact]
    public async Task DeleteFolderAsync_WithoutDeleteContents_ReparentsChildrenAndDocuments()
    {
        var parent = await _folderService.AddFolderAsync(new DocumentFolder { Name = "Parent" });
        var child = await _folderService.AddFolderAsync(new DocumentFolder { Name = "Child", ParentId = parent.Id });
        var document = await _documentService.AddDocumentAsync(
            new VaultDocument { FileName = "note.txt", ContentType = "text/plain", FolderId = child.Id },
            new MemoryStream([1]));

        await _folderService.DeleteFolderAsync(child.Id, deleteContents: false);

        var remaining = await _folderService.GetAllFoldersAsync();
        Assert.DoesNotContain(remaining, f => f.Id == child.Id);

        var reloadedDocument = await _documentService.GetDocumentAsync(document.Id);
        Assert.NotNull(reloadedDocument);
        Assert.Equal(parent.Id, reloadedDocument!.FolderId);
    }

    [Fact]
    public async Task DeleteFolderAsync_WithDeleteContents_RemovesChildrenAndDocuments()
    {
        var parent = await _folderService.AddFolderAsync(new DocumentFolder { Name = "Parent" });
        var child = await _folderService.AddFolderAsync(new DocumentFolder { Name = "Child", ParentId = parent.Id });
        var document = await _documentService.AddDocumentAsync(
            new VaultDocument { FileName = "note.txt", ContentType = "text/plain", FolderId = child.Id },
            new MemoryStream([1]));

        await _folderService.DeleteFolderAsync(parent.Id, deleteContents: true);

        var remaining = await _folderService.GetAllFoldersAsync();
        Assert.DoesNotContain(remaining, f => f.Id == parent.Id || f.Id == child.Id);
        Assert.Null(await _documentService.GetDocumentAsync(document.Id));
    }
}
