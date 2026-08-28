using System;

namespace PasswordVault.Models;

public enum AiProvider
{
    None = 0,
    Ollama = 1,
    OpenAI = 2,
    Gemini = 3,
    Anthropic = 4,
    Groq = 5,
    Mistral = 6,
    OpenRouter = 7
}


public record ProviderInfo(AiProvider Provider, string DisplayName, string DefaultModel, bool IsFree, string Tip);

public static class ProviderCatalog
{
    public static readonly ProviderInfo[] All =
    [
        new(AiProvider.Ollama, "Ollama (Local)", "llama3.2:3b", true, "100% free, runs on your machine, nothing leaves your PC"),
        new(AiProvider.Groq, "Groq", "openai/gpt-oss-120b", false, "Free tier with very fast responses"),
        new(AiProvider.Gemini, "Google Gemini", "gemini-2.5-flash", false, "Generous free tier"),
        new(AiProvider.OpenAI, "OpenAI", "gpt-4o-mini", false, "Paid, pay-as-you-go"),
        new(AiProvider.Anthropic, "Anthropic", "claude-haiku-4-5", false, "Paid, pay-as-you-go"),
        new(AiProvider.Mistral, "Mistral", "mistral-small-latest", false, "Free tier available for testing"),
    ];

    public static ProviderInfo? Get(AiProvider provider) => Array.Find(All, p => p.Provider == provider);
}

/// <summary>
/// Persisted AI configuration. Stored as encrypted JSON alongside user.dat.
/// </summary>
public class AiSettings
{
    public AiProvider Provider { get; set; } = AiProvider.None;
    public string OllamaEndpoint { get; set; } = "http://localhost:11434";
    public string OllamaModelName { get; set; } = "llama3.2:3b";
    public string ApiKey { get; set; } = string.Empty;
    public string CloudModelName { get; set; } = string.Empty;
    public bool HasUserAcceptedCloudPrivacyWarning { get; set; } = false;
    public bool SendTitle { get; set; } = true;
    public bool SendUrl { get; set; } = true;
    public bool SendUsername { get; set; } = false;


    public bool AutoSuggestOnNewEntry { get; set; } = false;
    public bool SuggestTags { get; set; } = true;
}
