using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using PasswordVault.Models;
using PasswordVault.Services.Crypto;
using PasswordVault.Services.Database;

namespace PasswordVault.Services.ImportExport;

public class ImportExportService(
    IPasswordService passwordService,
    ICategoryService categoryService,
    ICryptoService cryptoService) : IImportExportService
{
    private readonly IPasswordService _passwordService =
        passwordService ?? throw new ArgumentNullException(nameof(passwordService));
    private readonly ICategoryService _categoryService =
        categoryService ?? throw new ArgumentNullException(nameof(categoryService));
    private readonly ICryptoService _cryptoService =
        cryptoService ?? throw new ArgumentNullException(nameof(cryptoService));

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // Export (refactored)
    public async Task<int> ExportToCsvAsync(string filePath)
    {
        var passwords = (await _passwordService.GetAllPasswordsAsync()).ToList();

        var exportData = passwords.Select(p => new CsvExportEntry
        {
            Title = p.Title,
            Username = p.Username,
            Password = DecryptSafe(p.EncryptedPassword),
            Url = p.Url,
            Notes = p.Notes,
            Category = p.Category?.Name,
            Tags = p.Tags != null ? string.Join("; ", p.Tags) : string.Empty,
            IsFavorite = p.IsFavorite,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt
        }).ToList();

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = CultureInfo.CurrentCulture.TextInfo.ListSeparator
        };

        using var writer = new StreamWriter(filePath);
        using var csv = new CsvWriter(writer, config);
        csv.WriteRecords(exportData);

        return passwords.Count;
    }

    // (refactored)
    public async Task<int> ExportToJsonAsync(string filePath)
    {
        var passwords = (await _passwordService.GetAllPasswordsAsync()).ToList();

        var exportEntries = passwords.Select(p => new ExportEntry
        {
            Title = p.Title,
            Username = p.Username,
            Password = DecryptSafe(p.EncryptedPassword),
            Url = p.Url,
            Notes = p.Notes,
            Category = p.Category?.Name,
            Tags = p.Tags ?? [],
            IsFavorite = p.IsFavorite,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt
        }).ToList();

        var export = new ExportDocument
        {
            Format = "PasswordVault",
            Version = "1.0",
            ExportedAt = DateTime.UtcNow,
            EntryCount = exportEntries.Count,
            Entries = exportEntries
        };

        var json = JsonSerializer.Serialize(export, JsonOptions);
        await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);
        return passwords.Count;
    }

    // Import (refactored)
    public async Task<ImportResult> ImportFromJsonAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        var doc = JsonSerializer.Deserialize<ExportDocument>(json, JsonOptions);

        if (doc?.Entries is null)
            return new ImportResult { Errors = ["Invalid or empty JSON file."] };

        return await ImportEntriesAsync(doc.Entries);
    }

    // (refactored)

    public async Task<CsvPreview> GetCsvPreviewAsync(string filePath)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            IgnoreBlankLines = true,
            TrimOptions = TrimOptions.Trim,
            MissingFieldFound = null,
            HeaderValidated = null
        };

        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, config);

        await csv.ReadAsync();
        csv.ReadHeader();

        var preview = new CsvPreview
        {
            Headers = csv.HeaderRecord?.ToList() ?? []
        };

        int count = 0;
        await foreach (var record in csv.GetRecordsAsync<dynamic>())
        {
            var dict = (IDictionary<string, object>)record;
            var row = preview.Headers
                .Select(h => dict.TryGetValue(h, out var val) ? val?.ToString() ?? "" : "")
                .ToList();
            preview.Rows.Add(row);
            if (++count >= 5) break;
        }

        return preview;
    }

    public async Task<ImportResult> ImportWithMappingAsync(string filePath, ImportMapping mapping)
    {
        var result = new ImportResult();
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            IgnoreBlankLines = true,
            TrimOptions = TrimOptions.Trim,
            MissingFieldFound = null,
            HeaderValidated = null
        };

        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, config);

        await csv.ReadAsync();
        csv.ReadHeader();

        var headers = csv.HeaderRecord;
        if (headers == null) return new ImportResult { Errors = ["CSV file has no header row."] };

        var entries = new List<ExportEntry>();

        await foreach (var record in csv.GetRecordsAsync<dynamic>())
        {
            try
            {
                var dict = (IDictionary<string, object>)record;

                var entry = new ExportEntry
                {
                    Title = GetMappedValue(dict, mapping.TitleHeader),
                    Username = GetMappedValue(dict, mapping.UsernameHeader),
                    Password = GetMappedValue(dict, mapping.PasswordHeader),
                    Url = GetMappedValue(dict, mapping.UrlHeader),
                    Notes = GetMappedValue(dict, mapping.NotesHeader),
                    Category = GetMappedValue(dict, mapping.CategoryHeader),
                    Tags = GetMappedValue(dict, mapping.TagsHeader)
                        ?.Split(';', StringSplitOptions.RemoveEmptyEntries)
                        .Select(t => t.Trim())
                        .ToList() ?? [],
                    IsFavorite = IsTrue(GetMappedValue(dict, mapping.IsFavoriteHeader))
                };

                if (!string.IsNullOrWhiteSpace(entry.Title))
                    entries.Add(entry);
                else
                    result.Skipped++;
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add($"Row error: {ex.Message}");
            }
        }

        var importResult = await ImportEntriesAsync(entries);
        importResult.Failed += result.Failed;
        importResult.Skipped += result.Skipped;
        importResult.Errors.AddRange(result.Errors);
        return importResult;
    }

    private async Task<ImportResult> ImportEntriesAsync(List<ExportEntry> entries)
    {
        var result = new ImportResult();
        var existingPasswords = (await _passwordService.GetAllPasswordsAsync()).ToList();
        var categories = (await _categoryService.GetAllCategoriesAsync()).ToList();

        foreach (var entry in entries)
        {
            try
            {
                // Duplicate check: same title + username + url
                var isDuplicate = existingPasswords.Any(p =>
                    string.Equals(p.Title, entry.Title, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(p.Username, entry.Username, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(p.Url, entry.Url, StringComparison.OrdinalIgnoreCase));

                if (isDuplicate)
                {
                    result.Skipped++;
                    continue;
                }

                // Resolve or create category
                Category? category = null;
                if (!string.IsNullOrWhiteSpace(entry.Category))
                {
                    category = categories.FirstOrDefault(c =>
                        string.Equals(c.Name, entry.Category, StringComparison.OrdinalIgnoreCase));

                    if (category is null)
                    {
                        category = await _categoryService.AddCategoryAsync(new Category
                        {
                            Name = entry.Category,
                            Color = "#9E9E9E",
                            Icon = "fa-solid fa-tag"
                        });
                        categories.Add(category);
                    }
                }

                var password = new Password
                {
                    Title = entry.Title ?? "Untitled",
                    Username = entry.Username,
                    EncryptedPassword = !string.IsNullOrEmpty(entry.Password)
                        ? _cryptoService.EncryptPassword(entry.Password)
                        : string.Empty,
                    Url = entry.Url,
                    Notes = entry.Notes,
                    Category = category,
                    Tags = entry.Tags ?? [],
                    IsFavorite = entry.IsFavorite,
                    CreatedAt = entry.CreatedAt != default ? entry.CreatedAt : DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                password.Category ??= await _categoryService.GetOrCreateUncategorizedCategoryAsync();

                await _passwordService.AddPasswordAsync(password);
                result.Imported++;
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add($"'{entry.Title}': {ex.Message}");
            }
        }

        return result;
    }

    private string DecryptSafe(string encrypted)
    {
        if (string.IsNullOrEmpty(encrypted)) return string.Empty;
        try { return _cryptoService.DecryptPassword(encrypted); }
        catch { return string.Empty; }
    }

    private static string? GetMappedValue(IDictionary<string, object> dict, string? header)
    {
        if (string.IsNullOrWhiteSpace(header)) return null;
        return dict.TryGetValue(header, out var val) ? val?.ToString() : null;
    }

    private static bool IsTrue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        value = value.Trim().ToLowerInvariant();
        return value == "true" || value == "1" || value == "yes";
    }
}
