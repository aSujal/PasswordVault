using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PasswordVault.Models;

namespace PasswordVault.Services.AI;

/// <summary>
/// Cloud AI provider supporting OpenAI, Google Gemini, and Anthropic APIs.
/// All use the chat completions pattern with a structured categorization prompt.
/// </summary>
public class CloudAiProvider
{
    private const int SingleMaxTokens = 150;
    private const int BatchMaxTokens = 2000;
    private const int TestMaxTokens = 20;

    private readonly HttpClient _http;

    public CloudAiProvider(IHttpClientFactory httpClientFactory)
    {
        _http = httpClientFactory.CreateClient("CloudAi");
    }

    private static string GetApiUrl(AiProvider provider, string model)
    {
        return provider switch
        {
            AiProvider.OpenAI => "https://api.openai.com/v1/chat/completions",
            AiProvider.Gemini => $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent",
            AiProvider.Anthropic => "https://api.anthropic.com/v1/messages",
            AiProvider.Groq => "https://api.groq.com/openai/v1/chat/completions",
            AiProvider.Mistral => "https://api.mistral.ai/v1/chat/completions",
            AiProvider.OpenRouter => "https://openrouter.ai/api/v1/chat/completions",
            _ => throw new ArgumentException($"Unsupported cloud provider: {provider}")
        };
    }


    public async Task<AiSuggestion> SuggestAsync(
        AiProvider provider, string apiKey, string model,
        string title, string? url, string? username,
        List<string> categories, bool suggestTags,
        AiSettings settings,
        CancellationToken ct = default)
    {
        var prompt = AiPromptBuilder.BuildSinglePrompt(title, url, username, categories, suggestTags, settings);
        var responseText = await CallProviderAsync(provider, apiKey, model, prompt, SingleMaxTokens, ct);
        return AiResponseParser.ParseSingleResponse(responseText, categories, suggestTags);
    }

    /// <summary>
    /// Send a batch categorization request covering multiple entries in one call.
    /// </summary>
    public async Task<Dictionary<int, AiSuggestion>> SuggestBatchAsync(
        AiProvider provider, string apiKey, string model,
        IReadOnlyList<Password> entries,
        List<string> categories, bool suggestTags,
        AiSettings settings,
        CancellationToken ct = default)
    {
        var prompt = AiPromptBuilder.BuildBatchPrompt(entries, categories, suggestTags, settings);
        var responseText = await CallProviderAsync(provider, apiKey, model, prompt, BatchMaxTokens, ct);
        return AiResponseParser.ParseBatchResponse(responseText, categories, suggestTags);
    }

    public async Task<(bool success, string message)> TestConnectionAsync(
        AiProvider provider, string apiKey, string model,
        CancellationToken ct = default)
    {
        try
        {
            var response = await CallProviderAsync(provider, apiKey, model, "Respond with only the word: OK", TestMaxTokens, ct);
            return (true, $"Connected successfully. Response: {response.Trim()[..Math.Min(50, response.Trim().Length)]}");
        }
        catch (HttpRequestException ex)
        {
            return (false, $"Connection failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            return (false, $"Error: {ex.Message}");
        }
    }

    private async Task<string> CallProviderAsync(
        AiProvider provider, string apiKey, string model, string prompt, int maxTokens, CancellationToken ct)
    {
        return provider switch
        {
            AiProvider.OpenAI or AiProvider.Groq or AiProvider.Mistral or AiProvider.OpenRouter =>
                await CallOpenAiCompatibleAsync(GetApiUrl(provider, model), apiKey, model, prompt, maxTokens, ct),
            AiProvider.Gemini => await CallGeminiAsync(apiKey, model, prompt, maxTokens, ct),
            AiProvider.Anthropic => await CallAnthropicAsync(apiKey, model, prompt, maxTokens, ct),
            _ => throw new ArgumentException($"Unsupported cloud provider: {provider}")
        };
    }

    private async Task<string> CallOpenAiCompatibleAsync(string url, string apiKey, string model, string prompt, int maxTokens, CancellationToken ct)
    {
        var requestBody = new
        {
            model,
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            temperature = 0.1,
            max_tokens = maxTokens
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0 &&
            choices[0].TryGetProperty("message", out var message) && message.TryGetProperty("content", out var contentProp))
        {
            return contentProp.GetString() ?? "Uncategorized";
        }
        return "Uncategorized";
    }

    private async Task<string> CallGeminiAsync(string apiKey, string model, string prompt, int maxTokens, CancellationToken ct)
    {
        var url = $"{GetApiUrl(AiProvider.Gemini, model)}?key={apiKey}";
        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.1,
                maxOutputTokens = maxTokens
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _http.PostAsync(url, content, ct);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(responseJson);

        if (doc.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0 &&
            candidates[0].TryGetProperty("content", out var contentEl) && contentEl.TryGetProperty("parts", out var parts) &&
            parts.GetArrayLength() > 0 && parts[0].TryGetProperty("text", out var textProp))
        {
            return textProp.GetString() ?? "Uncategorized";
        }
        return "Uncategorized";
    }

    private async Task<string> CallAnthropicAsync(string apiKey, string model, string prompt, int maxTokens, CancellationToken ct)
    {
        var url = GetApiUrl(AiProvider.Anthropic, model);
        var requestBody = new
        {
            model,
            max_tokens = maxTokens,
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("content", out var contentArray) && contentArray.GetArrayLength() > 0 &&
            contentArray[0].TryGetProperty("text", out var textProp))
        {
            return textProp.GetString() ?? "Uncategorized";
        }
        return "Uncategorized";
    }
}
