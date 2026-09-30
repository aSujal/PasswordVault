using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PasswordVault.Models;

namespace PasswordVault.Services.AI;

public interface IAiCategorizationService
{
    AiSettings Settings { get; }

    bool IsConfigured { get; }

    event EventHandler? SettingsChanged;

    void SaveSettings(AiSettings settings);

    /// <summary>
    /// Returns the best matching category name for each entry, or null where none fits.
    /// Only titles and URLs are sent to the provider.
    /// </summary>
    Task<string?[]> CategorizeAsync(
        IReadOnlyList<(string Title, string? Url)> entries,
        IReadOnlyList<string> categories,
        Action<int>? onProgress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Downloads the configured Ollama model, reporting percent complete.
    /// </summary>
    Task PullOllamaModelAsync(Action<int> onProgress, CancellationToken ct = default);
}
