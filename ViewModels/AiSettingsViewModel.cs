using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;
using PasswordVault.Models;
using PasswordVault.Services.AI;

namespace PasswordVault.ViewModels;

public partial class AiSettingsViewModel : ViewModelBase
{
    private readonly IAiCategorizationService _aiService;
    private readonly bool _isLoading;

    public ProviderInfo[] Providers => ProviderCatalog.All;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEnabled), nameof(IsOllama), nameof(IsCloud), nameof(Description))]
    private ProviderInfo _selectedProvider = ProviderCatalog.All[0];

    [ObservableProperty] private string _model = string.Empty;
    [ObservableProperty] private string _apiKey = string.Empty;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private bool _isStatusError;

    public bool IsEnabled => SelectedProvider.Provider != AiProvider.None;
    public bool IsOllama => SelectedProvider.Provider == AiProvider.Ollama;
    public bool IsCloud => IsEnabled && !IsOllama;

    public string Description => SelectedProvider.Provider switch
    {
        AiProvider.None => "Suggest categories for your entries using a local or cloud model.",
        AiProvider.Ollama => "Runs on this PC, nothing leaves your device. Requires Ollama from ollama.com.",
        _ => $"Entry titles and URLs are sent to {SelectedProvider.Name}. Passwords, usernames and notes never are."
    };

    public AiSettingsViewModel(IAiCategorizationService aiService)
    {
        _aiService = aiService;

        _isLoading = true;
        SelectedProvider = ProviderCatalog.Get(aiService.Settings.Provider);
        Model = aiService.Settings.Model;
        ApiKey = aiService.Settings.ApiKey;
        _isLoading = false;
    }

    partial void OnSelectedProviderChanged(ProviderInfo value)
    {
        if (_isLoading) return;
        Status = string.Empty;
        Model = string.Empty; // A model name only makes sense for the provider it was typed for
        Save();
    }

    partial void OnModelChanged(string value) => Save();
    partial void OnApiKeyChanged(string value) => Save();

    private void Save()
    {
        if (_isLoading) return;
        _aiService.SaveSettings(new AiSettings { Provider = SelectedProvider.Provider, Model = Model, ApiKey = ApiKey });
    }

    [RelayCommand]
    private async Task Test()
    {
        SetStatus("Testing…", false);
        try
        {
            var result = await _aiService.CategorizeAsync([("Netflix", "netflix.com")], ["Email", "Entertainment", "Finance"]);
            SetStatus(result[0] is { } category
                ? $"Working. Netflix was filed under {category}."
                : "Connected, but the model didn't pick a category. Try a larger model.", result[0] is null);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, true);
        }
    }

    [RelayCommand]
    private async Task DownloadModel()
    {
        var model = _aiService.Settings.EffectiveModel;
        SetStatus($"Downloading {model}…", false);
        try
        {
            await _aiService.PullOllamaModelAsync(percent => SetStatus($"Downloading {model}… {percent}%", false));
            SetStatus($"{model} is ready.", false);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, true);
        }
    }

    private void SetStatus(string message, bool isError)
    {
        Status = message;
        IsStatusError = isError;
    }
}
