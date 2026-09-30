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
    /// Returns a category name for each entry: an existing one where it fits, otherwise a newly
    /// proposed name (not created here), or null if the model couldn't tell what the entry is.
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
