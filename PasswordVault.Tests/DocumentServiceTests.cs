using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PasswordVault.Models;
using PasswordVault.Services.Crypto;
using PasswordVault.Services.Database;
using Xunit;

namespace PasswordVault.Tests.Services;

// Documents live in their own "documents" collection, separate from passwords. Linking to a
// password (via PasswordId) is optional, not a requirement. File bytes are stored in LiteDB
// FileStorage under VaultDocument.StorageId, not inline on the row.
public class DocumentServiceTests : IDisposable
{
    private readonly string _tempFolder;
    private readonly DatabaseService _db;
    private readonly PasswordService _passwordService;
    private readonly DocumentService _documentService;
    private readonly Category _category;

    public DocumentServiceTests()
    {
        _tempFolder = Path.Combine(Path.GetTempPath(), "PasswordVaultTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempFolder);

        var cryptoService = new CryptoService(new byte[32]);
        _db = new DatabaseService(cryptoService, _tempFolder);
        _documentService = new DocumentService(_db);
        _passwordService = new PasswordService(_db, cryptoService, _documentService);

        _db.InitializeDatabaseAsync("master-password").GetAwaiter().GetResult();
        _category = new Category { Name = "Uncategorized" };
        _db.OpenDatabase().GetCollection<Category>("categories").Insert(_category);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempFolder, recursive: true); } catch { /* best effort cleanup */ }
    }

    private static Stream Bytes(params byte[] data) => new MemoryStream(data);

    [Fact]
    public async Task AddDocumentAsync_WithoutPasswordId_CreatesStandaloneDocument()
    {
        var document = await _documentService.AddDocumentAsync(new VaultDocument
        {
            FileName = "passport.pdf",
            ContentType = "application/pdf",
        }, Bytes(1, 2, 3));

        Assert.Null(document.PasswordId);
        Assert.False(string.IsNullOrEmpty(document.StorageId));
    }

    [Fact]
    public async Task AddDocumentAsync_LinkedToPassword_RoundTripsViaGetDocumentsForPassword()
    {
        var password = await _passwordService.AddPasswordAsync(new Password
        {
            Title = "Bank",
            EncryptedPassword = "irrelevant",
            Category = _category
        });

        await _documentService.AddDocumentAsync(new VaultDocument
        {
            PasswordId = password.Id,
            FileName = "id-card.pdf",
            ContentType = "application/pdf",
        }, Bytes(1, 2, 3));

        var reloaded = await _documentService.GetDocumentsForPasswordAsync(password.Id);

        Assert.Single(reloaded);
        Assert.Equal("id-card.pdf", reloaded[0].FileName);

        using var content = await _documentService.OpenDocumentAsync(reloaded[0].Id);
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms);
        Assert.Equal(new byte[] { 1, 2, 3 }, ms.ToArray());
    }

    [Fact]
    public async Task UpdateDocumentAsync_RenameAndMove_PreservesContent()
    {
        var document = await _documentService.AddDocumentAsync(new VaultDocument
        {
            FileName = "original.txt",
            ContentType = "text/plain",
        }, Bytes(4, 5, 6));

        document.FileName = "renamed.txt";
        document.FolderId = Guid.NewGuid();
        await _documentService.UpdateDocumentAsync(document);

        var reloaded = await _documentService.GetDocumentAsync(document.Id);
        Assert.NotNull(reloaded);
        Assert.Equal("renamed.txt", reloaded!.FileName);

        using var content = await _documentService.OpenDocumentAsync(document.Id);
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms);
        Assert.Equal(new byte[] { 4, 5, 6 }, ms.ToArray());
    }

    [Fact]
    public async Task SoftDelete_HidesFromDefaultQuery_RestoreBringsItBack()
    {
        var document = await _documentService.AddDocumentAsync(new VaultDocument
        {
            FileName = "note.txt",
            ContentType = "text/plain",
        }, Bytes(1));

        await _documentService.SoftDeleteDocumentAsync(document.Id);

        var active = await _documentService.GetDocumentsAsync(new DocumentQuery());
        Assert.DoesNotContain(active, d => d.Id == document.Id);

        var trash = await _documentService.GetDocumentsAsync(new DocumentQuery { Trash = true });
        Assert.Contains(trash, d => d.Id == document.Id);

        await _documentService.RestoreDocumentAsync(document.Id);

        active = await _documentService.GetDocumentsAsync(new DocumentQuery());
        Assert.Contains(active, d => d.Id == document.Id);
    }

    [Fact]
    public async Task PermanentlyDeleteDocumentAsync_RemovesRowAndFileStorageEntry()
    {
        var document = await _documentService.AddDocumentAsync(new VaultDocument
        {
            FileName = "gone.txt",
            ContentType = "text/plain",
        }, Bytes(1, 2));

        await _documentService.PermanentlyDeleteDocumentAsync(document.Id);

        Assert.Null(await _documentService.GetDocumentAsync(document.Id));
        Assert.Null(_db.OpenDatabase().FileStorage.FindById(document.StorageId));
    }

    [Fact]
    public async Task DeletingPassword_CascadesToItsDocuments()
    {
        var password = await _passwordService.AddPasswordAsync(new Password
        {
            Title = "Utility",
            EncryptedPassword = "irrelevant",
            Category = _category
        });

        var document = await _documentService.AddDocumentAsync(new VaultDocument
        {
            PasswordId = password.Id,
            FileName = "bill.pdf",
            ContentType = "application/pdf",
        }, Bytes(1));

        await _passwordService.DeletePasswordAsync(password.Id);

        Assert.Null(await _documentService.GetDocumentAsync(document.Id));
    }

    [Fact]
    public async Task SearchDocumentsAsync_MatchesFileNameTagAndNotes()
    {
        await _documentService.AddDocumentAsync(new VaultDocument
        {
            FileName = "passport-scan.pdf",
            ContentType = "application/pdf",
            Tags = ["identity"],
            Notes = "Renew before 2030",
        }, Bytes(1));

        await _documentService.AddDocumentAsync(new VaultDocument
        {
            FileName = "recipe.txt",
            ContentType = "text/plain",
        }, Bytes(2));

        var byName = await _documentService.GetDocumentsAsync(new DocumentQuery { Search = "passport" });
        Assert.Single(byName);

        var byTag = await _documentService.GetDocumentsAsync(new DocumentQuery { Search = "identity" });
        Assert.Single(byTag);

        var byNotes = await _documentService.GetDocumentsAsync(new DocumentQuery { Search = "renew" });
        Assert.Single(byNotes);
    }

    [Fact]
    public async Task AddDocumentFromFileAsync_UnderLimit_Succeeds()
    {
        var path = Path.Combine(_tempFolder, "small.bin");
        await File.WriteAllBytesAsync(path, new byte[10]);

        var document = await _documentService.AddDocumentFromFileAsync(path, null, null);
        Assert.Equal(10, document.SizeBytes);
    }

    [Fact]
    public async Task AddDocumentFromFileAsync_OverLimit_ThrowsClearError()
    {
        var path = Path.Combine(_tempFolder, "big.bin");
        using (var stream = File.Create(path))
        {
            stream.SetLength(DocumentService.MaxDocumentSizeBytes + 1);  // sparse - fast on NTFS
        }

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _documentService.AddDocumentFromFileAsync(path, null, null));
        Assert.Contains("50 MB", ex.Message);
    }
}
