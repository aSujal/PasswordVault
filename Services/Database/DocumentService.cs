using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PasswordVault.Models;

namespace PasswordVault.Services.Database;

public interface IDocumentService
{
    Task<List<DocumentAttachment>> GetDocumentsForPasswordAsync(Guid passwordId);
    Task<DocumentAttachment> AddDocumentAsync(DocumentAttachment document);
}

public class DocumentService(DatabaseService databaseService) : IDocumentService
{
    private const string CollectionName = "documents";

    private readonly DatabaseService _databaseService = databaseService;

    public async Task<List<DocumentAttachment>> GetDocumentsForPasswordAsync(Guid passwordId)
    {
        return await Task.Run(() =>
        {
            var db = _databaseService.OpenDatabase();
            var collection = db.GetCollection<DocumentAttachment>(CollectionName);
            return collection.Query().Where(d => d.PasswordId == passwordId).ToList();
        });
    }

    public async Task<DocumentAttachment> AddDocumentAsync(DocumentAttachment document)
    {
        return await Task.Run(() =>
        {
            var db = _databaseService.OpenDatabase();
            var collection = db.GetCollection<DocumentAttachment>(CollectionName);
            document.Id = Guid.NewGuid();
            collection.Insert(document);
            return document;
        });
    }
}
