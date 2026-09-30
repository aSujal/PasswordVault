using System;
using System.Text.Json.Serialization;

namespace PasswordVault.Models;

public enum AiProvider
{
    None = 0,
    Ollama = 1,
    OpenAI = 2,
    Gemini = 3,
    Anthropic = 4,
    Groq = 5,
    Mistral = 6
}

/// <summary>
/// Every provider is called through its OpenAI-compatible chat completions endpoint.
/// </summary>
public record ProviderInfo(AiProvider Provider, string Name, string Endpoint, string DefaultModel);

public static class ProviderCatalog
{
    public const string OllamaHost = "http://localhost:11434";

    public static readonly ProviderInfo[] All =
    [
        new(AiProvider.None, "Off", "", ""),
        new(AiProvider.Ollama, "Ollama (local)", $"{OllamaHost}/v1/chat/completions", "qwen3.5:4b"),
        new(AiProvider.Gemini, "Google Gemini", "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions", "gemini-2.5-flash-lite"),
        new(AiProvider.Groq, "Groq", "https://api.groq.com/openai/v1/chat/completions", "openai/gpt-oss-20b"),
        new(AiProvider.OpenAI, "OpenAI", "https://api.openai.com/v1/chat/completions", "gpt-5-nano"),
        new(AiProvider.Anthropic, "Anthropic", "https://api.anthropic.com/v1/chat/completions", "claude-haiku-4-5"),
        new(AiProvider.Mistral, "Mistral", "https://api.mistral.ai/v1/chat/completions", "mistral-small-latest"),
    ];

    public static ProviderInfo Get(AiProvider provider) => Array.Find(All, p => p.Provider == provider) ?? All[0];
}

/// <summary>
/// Persisted AI configuration. Stored as encrypted JSON alongside user.dat.
/// </summary>
public class AiSettings
{
    public AiProvider Provider { get; set; } = AiProvider.None;
    public string Model { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;

    [JsonIgnore]
    public string EffectiveModel => string.IsNullOrWhiteSpace(Model) ? ProviderCatalog.Get(Provider).DefaultModel : Model.Trim();
}
