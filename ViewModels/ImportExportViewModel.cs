using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using PasswordVault.Services.ImportExport;
using ShadUI;

namespace PasswordVault.ViewModels;

public partial class ImportExportViewModel(
    IImportExportService importExportService,
    ToastManager toastManager,
    DialogManager dialogManager,
    ImportMappingViewModel mappingViewModel) : ViewModelBase
{
    private readonly IImportExportService _importExportService = importExportService ?? throw new ArgumentNullException(nameof(importExportService));
    public readonly ToastManager _toastManager = toastManager ?? throw new ArgumentNullException(nameof(toastManager));
    private readonly DialogManager _dialogManager = dialogManager ?? throw new ArgumentNullException(nameof(dialogManager));
    private readonly ImportMappingViewModel _mappingViewModel = mappingViewModel ?? throw new ArgumentNullException(nameof(mappingViewModel));

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public async Task ExportCsvAsync(string filePath)
    {
        IsBusy = true;
        StatusMessage = "Exporting to CSV...";
        try
        {
            var count = await _importExportService.ExportToCsvAsync(filePath);
            StatusMessage = $"Exported {count} passwords to CSV.";
            _toastManager.CreateToast("Export Complete")
                .WithContent($"Successfully exported {count} passwords to CSV.")
                .Show();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
            _toastManager.CreateToast("Export Failed")
                .WithContent(ex.Message)
                .ShowError();
        }
        finally { IsBusy = false; }
    }

    public async Task ExportJsonAsync(string filePath)
    {
        IsBusy = true;
        StatusMessage = "Exporting to JSON...";
        try
        {
            var count = await _importExportService.ExportToJsonAsync(filePath);
            StatusMessage = $"Exported {count} passwords to JSON.";
            _toastManager.CreateToast("Export Complete")
                .WithContent($"Successfully exported {count} passwords to JSON.")
                .Show();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
            _toastManager.CreateToast("Export Failed")
                .WithContent(ex.Message)
                .ShowError();
        }
        finally { IsBusy = false; }
    }

    public async Task ImportAsync(string filePath)
    {
        var extension = System.IO.Path.GetExtension(filePath).ToLowerInvariant();

        IsBusy = true;
        StatusMessage = "Importing...";
        try
        {
            if (extension == ".json")
            {
                var result = await _importExportService.ImportFromJsonAsync(filePath);
                ShowImportResult(result);
                IsBusy = false;
            }
            else
            {
                var preview = await _importExportService.GetCsvPreviewAsync(filePath);
                var suggestion = CsvRowParsers.SuggestMapping(preview.Headers.ToArray());

                _mappingViewModel.Initialize(preview, suggestion);
                IsBusy = false;

                _dialogManager.CreateDialog(_mappingViewModel)
                    .WithMinWidth(700)
                    .WithSuccessCallback(async () =>
                    {
                        IsBusy = true;
                        StatusMessage = "Importing...";
                        try
                        {
                            var mapping = _mappingViewModel.GetMapping();
                            var result = await _importExportService.ImportWithMappingAsync(filePath, mapping);
                            ShowImportResult(result);
                        }
                        catch (Exception ex)
                        {
                            StatusMessage = $"Import failed: {ex.Message}";
                            _toastManager.CreateToast("Import Failed")
                                .WithContent(ex.Message)
                                .ShowError();
                        }
                        finally
                        {
                            IsBusy = false;
                        }
                    })
                    .WithCancelCallback(() =>
                    {
                        StatusMessage = "Import cancelled.";
                    })
                    .Show();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Import failed: {ex.Message}";
            _toastManager.CreateToast("Import Failed")
                .WithContent(ex.Message)
                .ShowError();
            IsBusy = false;
        }
    }

    private void ShowImportResult(ImportResult result)
    {
        StatusMessage = $"Imported: {result.Imported} | Skipped: {result.Skipped} | Failed: {result.Failed}";

        if (result.Imported > 0)
        {
            _toastManager.CreateToast("Import Complete")
                .WithContent($"Successfully imported {result.Imported} passwords.")
                .Show();
        }
        else if (result.Skipped > 0 && result.Imported == 0)
        {
            _toastManager.CreateToast("Nothing New")
                .WithContent($"All {result.Skipped} entries already exist in your vault.")
                .Show();
        }

        if (result.Failed > 0)
        {
            _toastManager.CreateToast("Import Warnings")
                .WithContent($"{result.Failed} entries failed to import.")
                .ShowError();
        }
    }
}
