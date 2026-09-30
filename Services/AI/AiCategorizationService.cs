using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using PasswordVault.Models;
using PasswordVault.Services.Crypto;

namespace PasswordVault.Services.AI;

public class AiCategorizationService : IAiCategorizationService
{
    private const int BatchSize = 20;
    private static readonly JsonSerializerOptions OmitNulls = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    private static readonly string SettingsFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PasswordManager", "ai_settings.dat");

    private readonly HttpClient _http;
    private readonly ICryptoService _cryptoService;

    public AiCategorizationService(IHttpClientFactory httpClientFactory, ICryptoService cryptoService)
    {
        _http = httpClientFactory.CreateClient("Ai");
        _cryptoService = cryptoService;
        Settings = LoadSettings();
    }

    public AiSettings Settings { get; private set; }

    public bool IsConfigured => Settings.Provider switch
    {
        AiProvider.None => false,
        AiProvider.Ollama => true,
        _ => !string.IsNullOrWhiteSpace(Settings.ApiKey)
    };

    public event EventHandler? SettingsChanged;

    // Synchronous so saves on every keystroke can never overlap on the file
    public void SaveSettings(AiSettings settings)
    {
        Settings = settings;
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
        File.WriteAllBytes(SettingsFile, _cryptoService.Encrypt(JsonSerializer.Serialize(settings)));
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private AiSettings LoadSettings()
    {
        if (!File.Exists(SettingsFile)) return new AiSettings();
        try
        {
            return JsonSerializer.Deserialize<AiSettings>(_cryptoService.Decrypt(File.ReadAllBytes(SettingsFile))) ?? new AiSettings();
        }
        catch (Exception)
        {
            return new AiSettings(); // Unreadable settings just leave AI switched off
        }
    }

    public async Task<string?[]> CategorizeAsync(
        IReadOnlyList<(string Title, string? Url)> entries,
        IReadOnlyList<string> categories,
        Action<int>? onProgress = null,
        CancellationToken ct = default)
    {
        // "Uncategorized" is what null means; offering it would let the model un-file entries
        var choices = categories.Where(c => !c.Equals("Uncategorized", StringComparison.OrdinalIgnoreCase)).ToList();
        var results = new string?[entries.Count];

        for (int start = 0; start < entries.Count; start += BatchSize)
        {
            var batch = entries.Skip(start).Take(BatchSize).ToList();
            var picks = await AskAsync(BuildPrompt(batch, choices), ct);

            for (int i = 0; i < batch.Count; i++)
            {
                var pick = picks.GetValueOrDefault((i + 1).ToString())?.Trim();
                if (string.IsNullOrEmpty(pick) || pick.Equals("Uncategorized", StringComparison.OrdinalIgnoreCase)) continue;

                var existing = choices.FirstOrDefault(c => c.Equals(pick, StringComparison.OrdinalIgnoreCase));
                if (existing == null) choices.Add(pick); // Later batches reuse the new name instead of inventing a variant
                results[start + i] = existing ?? pick;
            }
            onProgress?.Invoke((start + batch.Count) * 100 / entries.Count);
        }
        return results;
    }

    private static string BuildPrompt(List<(string Title, string? Url)> entries, List<string> categories)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Assign each password manager entry to a category.");
        sb.AppendLine($"Existing categories: {string.Join(", ", categories.Select(c => $"\"{c}\""))}");
        sb.AppendLine("Entries:");
        for (int i = 0; i < entries.Count; i++)
        {
            var (title, url) = entries[i];
            sb.AppendLine(string.IsNullOrWhiteSpace(url) ? $"{i + 1}. {title}" : $"{i + 1}. {title} ({url})");
        }
        sb.AppendLine();
        sb.AppendLine("Use an existing category whenever one fits, copying its name exactly. If none fits, invent a short new category " +
                      "name (1-2 words, Title Case) and reuse it for similar entries. Use null only if you can't tell what the entry is.");
        sb.Append("Reply with only a JSON object mapping each entry number to a category name. Example: {\"1\": \"")
          .Append(categories.FirstOrDefault() ?? "Email").Append("\", \"2\": null}");
        return sb.ToString();
    }

    private async Task<Dictionary<string, string?>> AskAsync(string prompt, CancellationToken ct)
    {
        var provider = ProviderCatalog.Get(Settings.Provider);
        var isLocal = Settings.Provider == AiProvider.Ollama;
        using var request = new HttpRequestMessage(HttpMethod.Post, provider.Endpoint)
        {
            Content = JsonContent.Create(new
            {
                model = Settings.EffectiveModel,
                messages = new[] { new { role = "user", content = prompt } },
                response_format = new { type = "json_object" },
                // Small local models drift off-format and misspell invented names when sampled, and "thinking"
                // makes each reply ~20x slower. Cloud reasoning models (e.g. gpt-5) reject both settings.
                temperature = isLocal ? 0 : (double?)null,
                reasoning_effort = isLocal ? "none" : null
            }, options: OmitNulls)
        };
        if (!string.IsNullOrWhiteSpace(Settings.ApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Settings.ApiKey.Trim());

        using var response = await SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var reply = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";

        // Providers without JSON mode (Anthropic) may wrap the object in prose or a code fence
        int start = reply.IndexOf('{'), end = reply.LastIndexOf('}');
        if (start < 0 || end < start)
            throw new InvalidOperationException($"{provider.Name} did not reply with JSON: {Shorten(reply)}");

        return JsonSerializer.Deserialize<Dictionary<string, string?>>(reply[start..(end + 1)]) ?? [];
    }

    public async Task PullOllamaModelAsync(Action<int> onProgress, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{ProviderCatalog.OllamaHost}/api/pull")
        {
            Content = JsonContent.Create(new { model = Settings.EffectiveModel })
        };
        using var response = await SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(ct));

        // Ollama streams one JSON status object per line until the download finishes
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var doc = JsonDocument.Parse(line);
            var status = doc.RootElement;

            if (status.TryGetProperty("error", out var error))
                throw new InvalidOperationException(error.GetString());
            if (status.TryGetProperty("total", out var total) && status.TryGetProperty("completed", out var completed) && total.GetInt64() > 0)
                onProgress((int)(completed.GetInt64() * 100 / total.GetInt64()));
        }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, HttpCompletionOption completion, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, completion, ct);
        }
        catch (HttpRequestException ex) when (Settings.Provider == AiProvider.Ollama && ex.HttpRequestError == HttpRequestError.ConnectionError)
        {
            throw new InvalidOperationException("Ollama isn't running. Install it from ollama.com and start it.", ex);
        }
        if (response.IsSuccessStatusCode) return response;

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"{ProviderCatalog.Get(Settings.Provider).Name}: {ErrorMessage(body)} ({(int)response.StatusCode})");
        }
    }

    // OpenAI-style APIs return {"error": {"message": ...}}, Ollama's native API {"error": "..."}
    private static string ErrorMessage(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement[0] : doc.RootElement;
            var error = root.GetProperty("error");
            return (error.ValueKind == JsonValueKind.String ? error.GetString() : error.GetProperty("message").GetString()) ?? body;
        }
        catch (Exception)
        {
            return Shorten(body);
        }
    }

    private static string Shorten(string text) => text.Length <= 200 ? text : text[..200] + "…";
}
