using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordVault.Helper;
using PasswordVault.Models;
using PasswordVault.Services.Database;
using ShadUI;

namespace PasswordVault.ViewModels;

public partial class DocumentPreviewViewModel : ViewModelBase
{
    // TODO: pdf preview
    private const int TextPreviewByteLimit = 200 * 1024;

    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp"];
    private static readonly string[] TextExtensions = [".txt", ".md", ".json", ".csv", ".xml", ".log"];

    private readonly DialogManager _dialogManager;
    private readonly IDocumentService _documentService;
    private readonly IDatabaseService _databaseService;
    private readonly SecureTempFileManager _tempFileManager;
    private readonly ToastManager _toastManager;

    [ObservableProperty] private VaultDocument? _document;
    [ObservableProperty] private Bitmap? _imageSource;
    [ObservableProperty] private string? _textContent;
    [ObservableProperty] private bool _isImage;
    [ObservableProperty] private bool _isText;
    [ObservableProperty] private bool _isOther;
    [ObservableProperty] private bool _isLoading;

    public DocumentPreviewViewModel(
        DialogManager dialogManager,
        IDocumentService documentService,
        IDatabaseService databaseService,
        SecureTempFileManager tempFileManager,
        ToastManager toastManager)
    {
        _dialogManager = dialogManager;
        _documentService = documentService;
        _databaseService = databaseService;
        _tempFileManager = tempFileManager;
        _toastManager = toastManager;
    }

    public async Task InitializeAsync(VaultDocument document)
    {
        Document = document;
        ImageSource = null;
        TextContent = null;
        IsImage = IsText = IsOther = false;
        IsLoading = true;

        try
        {
            var extension = Path.GetExtension(document.FileName).ToLowerInvariant();
            using var stream = await _documentService.OpenDocumentAsync(document.Id);

            if (Array.IndexOf(ImageExtensions, extension) >= 0)
            {
                ImageSource = new Bitmap(stream);
                IsImage = true;
            }
            else if (Array.IndexOf(TextExtensions, extension) >= 0)
            {
                using var reader = new StreamReader(stream);
                var buffer = new char[TextPreviewByteLimit];
                int read = await reader.ReadBlockAsync(buffer, 0, buffer.Length);
                TextContent = new string(buffer, 0, read);
                IsText = true;
            }
            else
            {
                IsOther = true;
            }
        }
        catch (Exception ex)
        {
            IsOther = true;
            _toastManager.CreateToast("Preview Failed").WithContent(ex.Message).ShowError();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task OpenExternally()
    {
        if (Document == null) return;

        var user = await _databaseService.GetUserAsync();
        if (user != null && !user.HasAcceptedDocumentOpenWarning)
        {
            _dialogManager.CreateDialog(
                "Open Outside PasswordVault",
                "This decrypts the file to a temporary location on disk so your default app can open it. It's shredded automatically when you lock the vault or close the app, but while the other app has it open it exists as plaintext on disk.")
                .WithPrimaryButton("Continue", async () =>
                {
                    user.HasAcceptedDocumentOpenWarning = true;
                    await _databaseService.UpdateUserAsync(user);
                    await OpenExternallyCore();
                })
                .WithCancelButton("Cancel")
                .Dismissible()
                .Show();
            return;
        }

        await OpenExternallyCore();
    }

    private async Task OpenExternallyCore()
    {
        if (Document == null) return;
        try
        {
            using var stream = await _documentService.OpenDocumentAsync(Document.Id);
            await _tempFileManager.OpenWithDefaultAppAsync(Document.FileName, stream);
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Could Not Open File").WithContent(ex.Message).ShowError();
        }
    }

    public async Task SaveAsAsync(string destinationPath)
    {
        if (Document == null) return;
        try
        {
            using var source = await _documentService.OpenDocumentAsync(Document.Id);
            await using var destination = File.Create(destinationPath);
            await source.CopyToAsync(destination);

            _toastManager.CreateToast("Saved").WithContent($"'{Document.FileName}' saved to disk.").ShowSuccess();
        }
        catch (Exception ex)
        {
            _toastManager.CreateToast("Save Failed").WithContent(ex.Message).ShowError();
        }
    }

    [RelayCommand]
    private void Close() => _dialogManager.Close(this);
}
