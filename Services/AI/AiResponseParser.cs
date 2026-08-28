using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using PasswordVault.Models;

namespace PasswordVault.Services.AI;

internal static class AiResponseParser
{
    public static AiSuggestion ParseSingleResponse(string responseText, List<string> categories, bool expectTags)
    {
        var suggestion = new AiSuggestion();

        if (expectTags)
        {
            try
            {
                using var doc = JsonDocument.Parse(StripMarkdownFence(responseText));
                var root = doc.RootElement;

                if (root.TryGetProperty("category", out var catProp))
                    suggestion.SuggestedCategory = catProp.GetString() ?? "Uncategorized";

                if (root.TryGetProperty("tags", out var tagsProp) && tagsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var tag in tagsProp.EnumerateArray())
                    {
                        var t = tag.GetString();
                        if (!string.IsNullOrWhiteSpace(t))
                            suggestion.SuggestedTags.Add(t.ToLowerInvariant());
                    }
                }
            }
            catch
            {
                // Fallback: treat entire response as category name
                suggestion.SuggestedCategory = responseText.Trim();
            }
        }
        else
        {
            suggestion.SuggestedCategory = responseText.Trim();
        }

        suggestion.SuggestedCategory = MatchCategory(suggestion.SuggestedCategory, categories);
        return suggestion;
    }

    public static Dictionary<int, AiSuggestion> ParseBatchResponse(string responseText, List<string> categories, bool expectTags)
    {
        var results = new Dictionary<int, AiSuggestion>();

        using var doc = JsonDocument.Parse(StripMarkdownFence(responseText));
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return results;

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            if (!item.TryGetProperty("id", out var idProp) || !idProp.TryGetInt32(out var id))
                continue;

            var suggestion = new AiSuggestion();
            if (item.TryGetProperty("category", out var catProp))
                suggestion.SuggestedCategory = catProp.GetString() ?? "Uncategorized";

            if (expectTags && item.TryGetProperty("tags", out var tagsProp) && tagsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var tag in tagsProp.EnumerateArray())
                {
                    var t = tag.GetString();
                    if (!string.IsNullOrWhiteSpace(t))
                        suggestion.SuggestedTags.Add(t.ToLowerInvariant());
                }
            }

            suggestion.SuggestedCategory = MatchCategory(suggestion.SuggestedCategory, categories);
            results[id] = suggestion;
        }

        return results;
    }

    private static string StripMarkdownFence(string responseText)
    {
        var cleaned = responseText.Trim();
        if (!cleaned.StartsWith("```")) return cleaned;

        var lines = cleaned.Split('\n');
        return string.Join('\n', lines.Skip(1).TakeWhile(l => !l.TrimStart().StartsWith("```")));
    }

    private static string MatchCategory(string suggestedCategory, List<string> categories)
    {
        var matched = categories.FirstOrDefault(c =>
            c.Equals(suggestedCategory, StringComparison.OrdinalIgnoreCase));
        return matched ?? "Uncategorized";
    }
}
