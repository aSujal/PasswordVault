using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PasswordVault.Models;

namespace PasswordVault.Services.AI;


public class AiSuggestion
{
    public string SuggestedCategory { get; set; } = "Uncategorized";
    public List<string> SuggestedTags { get; set; } = [];
}


public interface IAiCategorizationService
{
    Task<AiSuggestion> SuggestCategoryAsync(
        string title,
        string? url,
        string? username,
        IEnumerable<string> existingCategories,
        CancellationToken ct = default);

    Task<List<(Password password, AiSuggestion suggestion)>> BulkSuggestAsync(
        IEnumerable<Password> passwords,
        IEnumerable<string> existingCategories,
        System.Action<int>? onProgress = null,
        CancellationToken ct = default);

    Task<(bool success, string message)> TestConnectionAsync(CancellationToken ct = default);

    Task<List<string>> GetAvailableModelsAsync(CancellationToken ct = default);

    bool IsConfigured { get; }
}
