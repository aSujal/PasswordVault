using System;
using System.Collections.Generic;

namespace PasswordVault.Services.ImportExport;

internal class ExportDocument
{
    public string Format { get; set; } = "PasswordVault";
    public string Version { get; set; } = "1.0";
    public DateTime ExportedAt { get; set; }
    public int EntryCount { get; set; }
    public List<ExportEntry> Entries { get; set; } = [];
}

internal class ExportEntry
{
    public string? Title { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? Url { get; set; }
    public string? Notes { get; set; }
    public string? Category { get; set; }
    public List<string> Tags { get; set; } = [];
    public bool IsFavorite { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ImportMapping
{
    public string? TitleHeader { get; set; }
    public string? UsernameHeader { get; set; }
    public string? PasswordHeader { get; set; }
    public string? UrlHeader { get; set; }
    public string? NotesHeader { get; set; }
    public string? CategoryHeader { get; set; }
    public string? TagsHeader { get; set; }
    public string? IsFavoriteHeader { get; set; }
}

public class CsvPreview
{
    public List<string> Headers { get; set; } = [];
    public List<List<string>> Rows { get; set; } = [];
}

public class ImportResult
{
    public int Imported { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public List<string> Errors { get; set; } = [];
}

internal class CsvExportEntry
{
    public string? Title { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? Url { get; set; }
    public string? Notes { get; set; }
    public string? Category { get; set; }
    public string? Tags { get; set; }
    public bool IsFavorite { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}