using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using PasswordVault.Models;
using PasswordVault.Services.AI;

namespace PasswordVault.ViewModels;

public partial class AiSettingsViewModel : ViewModelBase
{
    private readonly AiSettingsService _aiSettingsService;
    private readonly IAiCategorizationService _aiCategorizationService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOllamaSelected))]
    [NotifyPropertyChangedFor(nameof(IsCloudSelected))]
    [NotifyPropertyChangedFor(nameof(IsAiEnabled))]
    [NotifyPropertyChangedFor(nameof(SelectedProviderInfo))]
    [NotifyPropertyChangedFor(nameof(SelectedProviderTip))]
    [NotifyPropertyChangedFor(nameof(SelectedProviderIsFree))]
    [NotifyPropertyChangedFor(nameof(SelectedProviderDisplayName))]
    private int _selectedProviderIndex; // matches AiProvider enum value

    public bool IsOllamaSelected => SelectedProviderIndex == (int)AiProvider.Ollama;
    public bool IsCloudSelected => IsAiEnabled && !IsOllamaSelected;
    public bool IsAiEnabled => SelectedProviderIndex > 0;

    public ObservableCollection<ProviderInfo> AvailableProviders { get; } = new(ProviderCatalog.All);

    public ProviderInfo? SelectedProviderInfo => ProviderCatalog.Get((AiProvider)SelectedProviderIndex);
    public string SelectedProviderTip => SelectedProviderInfo?.Tip ?? string.Empty;
    public bool SelectedProviderIsFree => SelectedProviderInfo?.IsFree ?? false;
    public string SelectedProviderDisplayName => SelectedProviderInfo?.DisplayName ?? "None (disabled)";

    // Ollama settings
    [ObservableProperty] private string _ollamaEndpoint = "http://localhost:11434";
    [ObservableProperty] private string _ollamaModelName = "llama3.2:3b";
    [ObservableProperty] private ObservableCollection<string> _availableOllamaModels = [];

    // Cloud settings
    [ObservableProperty] private string _apiKey = string.Empty;
    [ObservableProperty] private string _cloudModelName = string.Empty;

    // Privacy & data controls
    [ObservableProperty] private bool _sendTitle = true;
    [ObservableProperty] private bool _sendUrl = true;
    [ObservableProperty] private bool _sendUsername = false;
    [ObservableProperty] private bool _hasAcceptedPrivacyWarning = false;

    // Behavior
    [ObservableProperty] private bool _autoSuggestOnNewEntry = false;
    [ObservableProperty] private bool _suggestTags = true;

    // Connection test
    [ObservableProperty] private string _connectionStatus = string.Empty;
    [ObservableProperty] private bool _isConnectionSuccess;
    [ObservableProperty] private bool _isTestingConnection;
    [ObservableProperty] private bool _hasConnectionResult;
    [ObservableProperty] private bool _isSavingAiSettings;

    public AiSettingsViewModel(AiSettingsService aiSettingsService, IAiCategorizationService aiCategorizationService)
    {
        _aiSettingsService = aiSettingsService;
        _aiCategorizationService = aiCategorizationService;

        _ = LoadAiSettingsAsync();
    }

    private async Task LoadAiSettingsAsync()
    {
        var settings = await _aiSettingsService.LoadAsync();

        SelectedProviderIndex = (int)settings.Provider;
        OllamaEndpoint = settings.OllamaEndpoint;
        OllamaModelName = settings.OllamaModelName;
        ApiKey = settings.ApiKey;
        CloudModelName = settings.CloudModelName;
        SendTitle = settings.SendTitle;
        SendUrl = settings.SendUrl;
        SendUsername = settings.SendUsername;
        HasAcceptedPrivacyWarning = settings.HasUserAcceptedCloudPrivacyWarning;
        AutoSuggestOnNewEntry = settings.AutoSuggestOnNewEntry;
        SuggestTags = settings.SuggestTags;
    }

    [RelayCommand]
    private void SelectProvider(AiProvider provider)
    {
        SelectedProviderIndex = (int)provider;
        if (provider != AiProvider.Ollama)
            CloudModelName = ProviderCatalog.Get(provider)?.DefaultModel ?? CloudModelName;
    }

    [RelayCommand]
    private async Task QuickSetupOllama()
    {
        SelectedProviderIndex = (int)AiProvider.Ollama;
        OllamaEndpoint = "http://localhost:11434";
        OllamaModelName = "llama3.2:3b";
        await SaveAiSettings();
        await TestAiConnection();
    }

    [RelayCommand]
    private async Task SaveAiSettings()
    {
        IsSavingAiSettings = true;
        try
        {
            var settings = new AiSettings
            {
                Provider = (AiProvider)SelectedProviderIndex,
                OllamaEndpoint = OllamaEndpoint,
                OllamaModelName = OllamaModelName,
                ApiKey = ApiKey,
                CloudModelName = CloudModelName,
                SendTitle = SendTitle,
                SendUrl = SendUrl,
                SendUsername = SendUsername,
                HasUserAcceptedCloudPrivacyWarning = HasAcceptedPrivacyWarning,
                AutoSuggestOnNewEntry = AutoSuggestOnNewEntry,
                SuggestTags = SuggestTags
            };

            await _aiSettingsService.SaveAsync(settings);
            ConnectionStatus = "Settings saved successfully.";
            IsConnectionSuccess = true;
            HasConnectionResult = true;
        }
        catch (Exception ex)
        {
            ConnectionStatus = $"Failed to save: {ex.Message}";
            IsConnectionSuccess = false;
            HasConnectionResult = true;
        }
        finally
        {
            IsSavingAiSettings = false;
        }
    }

    [RelayCommand]
    private async Task TestAiConnection()
    {
        if (IsTestingConnection) return;
        IsTestingConnection = true;
        HasConnectionResult = false;

        try
        {
            // Temporarily save so the service can use the current config
            await SaveAiSettings();

            var (success, message) = await _aiCategorizationService.TestConnectionAsync();
            ConnectionStatus = message;
            IsConnectionSuccess = success;
            HasConnectionResult = true;
        }
        catch (Exception ex)
        {
            ConnectionStatus = $"Error: {ex.Message}";
            IsConnectionSuccess = false;
            HasConnectionResult = true;
        }
        finally
        {
            IsTestingConnection = false;
        }
    }

    [RelayCommand]
    private async Task RefreshOllamaModels()
    {
        try
        {
            // Ensure settings are saved first so the service reads the current endpoint
            await SaveAiSettings();

            var models = await _aiCategorizationService.GetAvailableModelsAsync();
            AvailableOllamaModels = new ObservableCollection<string>(models);

            if (models.Count > 0 && !models.Contains(OllamaModelName))
            {
                OllamaModelName = models[0];
            }
        }
        catch (Exception ex)
        {
            ConnectionStatus = $"Failed to list models: {ex.Message}";
            IsConnectionSuccess = false;
            HasConnectionResult = true;
        }
    }

    [RelayCommand]
    private void AcceptPrivacyWarning()
    {
        HasAcceptedPrivacyWarning = true;
    }
}
