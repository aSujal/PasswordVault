using System.Collections.Generic;
using System.Text;
using PasswordVault.Models;

namespace PasswordVault.Services.AI;

internal static class AiPromptBuilder
{
    public static string BuildSinglePrompt(
        string title, string? url, string? username,
        List<string> categories, bool suggestTags,
        AiSettings settings)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a password vault assistant. Your ONLY job is to categorize entries.");
        sb.AppendLine();
        AppendCategoryList(sb, categories);
        sb.AppendLine("Entry details:");
        AppendEntryDetails(sb, title, url, username, settings);
        sb.AppendLine();
        AppendResponseInstructions(sb, suggestTags);

        return sb.ToString();
    }

    public static string BuildBatchPrompt(
        IReadOnlyList<Password> entries,
        List<string> categories, bool suggestTags,
        AiSettings settings)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a password vault assistant. Your ONLY job is to categorize entries.");
        sb.AppendLine();
        AppendCategoryList(sb, categories);
        sb.AppendLine("Entries:");
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            sb.AppendLine($"Entry {i + 1}:");
            AppendEntryDetails(sb, entry.Title, entry.Url, entry.Username, settings);
        }
        sb.AppendLine();

        sb.AppendLine("Respond ONLY with a valid JSON array, one object per entry, in this exact format (no other text):");
        sb.AppendLine(suggestTags
            ? "[{\"id\": 1, \"category\": \"<exact category name from the list above>\", \"tags\": [\"tag1\", \"tag2\"]}, ...]"
            : "[{\"id\": 1, \"category\": \"<exact category name from the list above>\"}, ...]");
        sb.AppendLine();
        sb.AppendLine("Rules:");
        sb.AppendLine("- \"id\" must match the entry number above.");
        sb.AppendLine("- Include one object per entry, in any order.");
        sb.AppendLine("- Pick the single best category from the list. If none fit, use \"Uncategorized\".");
        if (suggestTags)
            sb.AppendLine("- Suggest 1-3 short lowercase tags that describe the entry (e.g., \"streaming\", \"finance\", \"2fa\").");
        sb.AppendLine("- Do NOT include any explanation, markdown, or extra text. ONLY the JSON array.");

        return sb.ToString();
    }

    private static void AppendCategoryList(StringBuilder sb, List<string> categories)
    {
        sb.AppendLine("Available categories:");
        foreach (var cat in categories) sb.AppendLine($"  - {cat}");
        sb.AppendLine();
    }

    private static void AppendEntryDetails(StringBuilder sb, string title, string? url, string? username, AiSettings settings)
    {
        if (settings.SendTitle && !string.IsNullOrWhiteSpace(title))
            sb.AppendLine($"  Title: {title}");
        if (settings.SendUrl && !string.IsNullOrWhiteSpace(url))
            sb.AppendLine($"  URL: {url}");
        if (settings.SendUsername && !string.IsNullOrWhiteSpace(username))
            sb.AppendLine($"  Username: {username}");
    }

    private static void AppendResponseInstructions(StringBuilder sb, bool suggestTags)
    {
        if (suggestTags)
        {
            sb.AppendLine("Respond ONLY with valid JSON in this exact format (no other text):");
            sb.AppendLine("{\"category\": \"<exact category name from the list above>\", \"tags\": [\"tag1\", \"tag2\"]}");
            sb.AppendLine();
            sb.AppendLine("Rules:");
            sb.AppendLine("- Pick the single best category from the list. If none fit, use \"Uncategorized\".");
            sb.AppendLine("- Suggest 1-3 short lowercase tags that describe the entry (e.g., \"streaming\", \"finance\", \"2fa\").");
            sb.AppendLine("- Do NOT include any explanation, markdown, or extra text. ONLY the JSON object.");
        }
        else
        {
            sb.AppendLine("Respond with ONLY the exact category name from the list above. Nothing else.");
            sb.AppendLine("If none of the categories fit, respond with: Uncategorized");
        }
    }
}
