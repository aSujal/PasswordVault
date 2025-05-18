using PasswordVault.Services.Sync;
using System;

namespace PasswordVault.ViewModels;

public partial class SyncViewModel : ViewModelBase
{
    private readonly SyncService _syncService;
    public string Greeting { get; } = "Welcome to Avalonia!";
    public SyncViewModel (SyncService syncService)
    {
        _syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
        //SyncCommand = ReactiveCommand.CreateFromTask(SyncAsync);
        //CancelSyncCommand = ReactiveCommand.Create(CancelSync);
    }
}
