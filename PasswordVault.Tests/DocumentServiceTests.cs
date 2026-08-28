using System;
using System.IO;
using System.Threading.Tasks;
using PasswordVault.Models;
using PasswordVault.Services.Crypto;
using PasswordVault.Services.Database;
using Xunit;

namespace PasswordVault.Tests.Services;

// Documents live in their own "documents" collection, separate from passwords. Linking to a
// password (via PasswordId) is optional, not a requirement.
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
        _passwordService = new PasswordService(_db, cryptoService);
        _documentService = new DocumentService(_db);

        _db.InitializeDatabaseAsync("master-password").GetAwaiter().GetResult();
        _category = new Category { Name = "Uncategorized" };
        _db.OpenDatabase().GetCollection<Category>("categories").Insert(_category);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempFolder, recursive: true); } catch { /* best effort cleanup */ }
    }

    [Fact]
    public async Task AddDocumentAsync_WithoutPasswordId_CreatesStandaloneDocument()
    {
        var document = await _documentService.AddDocumentAsync(new DocumentAttachment
        {
            FileName = "passport.pdf",
            ContentType = "pdf",
            SizeBytes = 3,
            Data = [1, 2, 3]
        });

        Assert.Null(document.PasswordId);
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

        await _documentService.AddDocumentAsync(new DocumentAttachment
        {
            PasswordId = password.Id,
            FileName = "id-card.pdf",
            ContentType = "pdf",
            SizeBytes = 3,
            Data = [1, 2, 3]
        });

        var reloaded = await _documentService.GetDocumentsForPasswordAsync(password.Id);

        Assert.Single(reloaded);
        Assert.Equal("id-card.pdf", reloaded[0].FileName);
        Assert.Equal(new byte[] { 1, 2, 3 }, reloaded[0].Data);
    }
}
