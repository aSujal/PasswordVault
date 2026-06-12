using System;
using System.Collections.Generic;
using System.Linq;

namespace PasswordVault.Services.ImportExport;

internal static class CsvRowParsers
{
    internal static ImportMapping SuggestMapping(string[] headers)
    {
        var mapping = new ImportMapping();
        var normalized = headers.Select(h => h.ToLowerInvariant()).ToArray();

        mapping.TitleHeader = GetMatch(normalized, headers, "title", "name", "entry", "site", "account");
        mapping.UsernameHeader = GetMatch(normalized, headers, "username", "user", "login", "email", "login_username", "login name", "loginname");
        mapping.PasswordHeader = GetMatch(normalized, headers, "password", "pass", "secret", "login_password");
        mapping.UrlHeader = GetMatch(normalized, headers, "url", "uri", "website", "site", "login_uri", "web site", "web_site");
        mapping.NotesHeader = GetMatch(normalized, headers, "notes", "note", "extra", "comments", "description");
        mapping.CategoryHeader = GetMatch(normalized, headers, "category", "folder", "group", "grouping", "type");
        mapping.TagsHeader = GetMatch(normalized, headers, "tags", "labels", "keywords");
        mapping.IsFavoriteHeader = GetMatch(normalized, headers, "favorite", "fav", "star");

        return mapping;
    }

    private static string? GetMatch(string[] normalized, string[] original, params string[] options)
    {
        for (int i = 0; i < normalized.Length; i++)
        {
            if (options.Any(opt => normalized[i].Contains(opt)))
                return original[i];
        }
        return null;
    }
}
