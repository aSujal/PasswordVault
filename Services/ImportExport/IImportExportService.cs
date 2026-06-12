using System.Threading.Tasks;

namespace PasswordVault.Services.ImportExport;

public interface IImportExportService
{
    Task<int> ExportToCsvAsync(string filePath);
    Task<int> ExportToJsonAsync(string filePath);
    Task<CsvPreview> GetCsvPreviewAsync(string filePath);
    Task<ImportResult> ImportWithMappingAsync(string filePath, ImportMapping mapping);
    Task<ImportResult> ImportFromJsonAsync(string filePath);
}
