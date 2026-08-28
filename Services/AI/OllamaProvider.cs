using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PasswordVault.Models;

namespace PasswordVault.Services.AI;

public class OllamaProvider
{
    private readonly HttpClient _http;

    public OllamaProvider(IHttpClientFactory httpClientFactory)
    {
        _http = httpClientFactory.CreateClient("Ollama");
    }

    public async Task<AiSuggestion> SuggestAsync(
        string endpoint, string model,
        string title, string? url, string? username,
        List<string> categories, bool suggestTags,
        AiSettings settings,
        CancellationToken ct = default)
    {
        var prompt = AiPromptBuilder.BuildSinglePrompt(title, url, username, categories, suggestTags, settings);
        var responseText = await GenerateAsync(endpoint, model, prompt, ct);
        return AiResponseParser.ParseSingleResponse(responseText, categories, suggestTags);
    }

    public async Task<Dictionary<int, AiSuggestion>> SuggestBatchAsync(
        string endpoint, string model,
        IReadOnlyList<Password> entries,
        List<string> categories, bool suggestTags,
        AiSettings settings,
        CancellationToken ct = default)
    {
        var prompt = AiPromptBuilder.BuildBatchPrompt(entries, categories, suggestTags, settings);
        var responseText = await GenerateAsync(endpoint, model, prompt, ct);
        return AiResponseParser.ParseBatchResponse(responseText, categories, suggestTags);
    }

    private async Task<string> GenerateAsync(string endpoint, string model, string prompt, CancellationToken ct)
    {
        var requestBody = new
        {
            model,
            prompt,
            stream = false,
            options = new { temperature = 0.1 }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _http.PostAsync($"{endpoint.TrimEnd('/')}/api/generate", content, ct);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(responseJson);
        return doc.RootElement.GetProperty("response").GetString()?.Trim() ?? "Uncategorized";
    }

    public async Task<(bool success, string message)> TestConnectionAsync(
        string endpoint, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync($"{endpoint.TrimEnd('/')}/api/tags", ct);
            if (response.IsSuccessStatusCode)
                return (true, "Connected to Ollama.");

            return (false, $"Ollama responded with status {(int)response.StatusCode}.");
        }
        catch (HttpRequestException ex)
        {
            return (false, $"Cannot reach Ollama at {endpoint}. ({ex.Message})");
        }
        catch (TaskCanceledException)
        {
            return (false, "Connection timed out.");
        }
    }

    public async Task<List<string>> GetAvailableModelsAsync(
        string endpoint, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"{endpoint.TrimEnd('/')}/api/tags", ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

        var models = new List<string>();
        if (doc.RootElement.TryGetProperty("models", out var modelsArray))
        {
            foreach (var model in modelsArray.EnumerateArray())
            {
                if (!model.TryGetProperty("name", out var nameProp)) continue;
                var name = nameProp.GetString();
                if (!string.IsNullOrEmpty(name))
                    models.Add(name);
            }
        }
        return models;
    }
}
