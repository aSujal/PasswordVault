using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PasswordVault.Models;

namespace PasswordVault.Services.AI;

public class AiCategorizationService(AiSettingsService settingsService, OllamaProvider ollama, CloudAiProvider cloud) : IAiCategorizationService
{
    private const int BatchSize = 20;

    private readonly AiSettingsService _settingsService = settingsService;
    private readonly OllamaProvider _ollama = ollama;
    private readonly CloudAiProvider _cloud = cloud;

    public bool IsConfigured
    {
        get
        {
            var settings = _settingsService.GetCurrent();
            if (settings == null || settings.Provider == AiProvider.None) return false;
            if (settings.Provider == AiProvider.Ollama) return true;
            return !string.IsNullOrWhiteSpace(settings.ApiKey);
        }
    }

    public async Task<AiSuggestion> SuggestCategoryAsync(
        string title, string? url, string? username,
        IEnumerable<string> existingCategories,
        CancellationToken ct = default)
    {
        var settings = await _settingsService.LoadAsync();
        if (settings.Provider == AiProvider.None)
            throw new InvalidOperationException("No AI provider configured. Go to Settings->AI Assistant.");

        var categories = existingCategories.ToList();

        return settings.Provider switch
        {
            AiProvider.Ollama => await _ollama.SuggestAsync(
                settings.OllamaEndpoint, settings.OllamaModelName,
                title, url, username, categories, settings.SuggestTags, settings, ct),

            _ => await _cloud.SuggestAsync(
                    settings.Provider, settings.ApiKey, settings.CloudModelName,
                    title, url, username, categories, settings.SuggestTags, settings, ct),
        };
    }

    public async Task<List<(Password password, AiSuggestion suggestion)>> BulkSuggestAsync(
        IEnumerable<Password> passwords,
        IEnumerable<string> existingCategories,
        Action<int>? onProgress = null,
        CancellationToken ct = default)
    {
        var settings = await _settingsService.LoadAsync();
        if (settings.Provider == AiProvider.None)
            throw new InvalidOperationException("No AI provider configured.");

        var passwordList = passwords.ToList();
        var categoryList = existingCategories.ToList();
        var results = new List<(Password, AiSuggestion)>(passwordList.Count);
        int completed = 0;

        foreach (var batch in passwordList.Chunk(BatchSize))
        {
            ct.ThrowIfCancellationRequested();

            Dictionary<int, AiSuggestion> suggestions;
            try
            {
                suggestions = settings.Provider == AiProvider.Ollama
                    ? await _ollama.SuggestBatchAsync(
                        settings.OllamaEndpoint, settings.OllamaModelName,
                        batch, categoryList, settings.SuggestTags, settings, ct)
                    : await _cloud.SuggestBatchAsync(
                        settings.Provider, settings.ApiKey, settings.CloudModelName,
                        batch, categoryList, settings.SuggestTags, settings, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException)
            {
                // Log but don't fail the whole run; this batch falls back to Uncategorized below
                Console.WriteLine($"AI batch categorization failed: {ex.Message}");
                suggestions = [];
            }

            for (int i = 0; i < batch.Length; i++)
            {
                var suggestion = suggestions.GetValueOrDefault(i + 1) ?? new AiSuggestion { SuggestedCategory = "Uncategorized" };
                results.Add((batch[i], suggestion));
            }

            completed += batch.Length;
            onProgress?.Invoke((int)(completed / (double)passwordList.Count * 100));
        }

        return results;
    }

    public async Task<(bool success, string message)> TestConnectionAsync(CancellationToken ct = default)
    {
        var settings = await _settingsService.LoadAsync();

        return settings.Provider switch
        {
            AiProvider.None => (false, "No AI provider configured."),
            AiProvider.Ollama => await _ollama.TestConnectionAsync(settings.OllamaEndpoint, ct),
            _ => await _cloud.TestConnectionAsync(settings.Provider, settings.ApiKey, settings.CloudModelName, ct),
        };
    }

    public async Task<List<string>> GetAvailableModelsAsync(CancellationToken ct = default)
    {
        var settings = await _settingsService.LoadAsync();
        if (settings.Provider != AiProvider.Ollama)
            return [];

        return await _ollama.GetAvailableModelsAsync(settings.OllamaEndpoint, ct);
    }
}
