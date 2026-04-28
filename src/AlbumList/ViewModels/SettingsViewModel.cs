using AlbumList.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AlbumList.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IExportService _csvExportService;
    private readonly DatabaseExportService _databaseExportService;
    private readonly CsvImportService _csvImportService;

    public SettingsViewModel(
        IExportService csvExportService,
        DatabaseExportService databaseExportService,
        CsvImportService csvImportService)
    {
        _csvExportService = csvExportService;
        _databaseExportService = databaseExportService;
        _csvImportService = csvImportService;
    }

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        await _csvExportService.ExportAsync(CancellationToken.None);
    }

    [RelayCommand]
    private async Task ExportDatabaseAsync()
    {
        await _databaseExportService.ExportAsync(CancellationToken.None);
    }

    [RelayCommand]
    private async Task ImportCsvAsync()
    {
        var result = await _csvImportService.ImportAsync(CancellationToken.None);
        var message = $"Imported: {result.Imported}  Merged: {result.Merged}  Skipped: {result.Skipped}";
        await Application.Current!.MainPage!.DisplayAlert("Import Complete", message, "OK");
    }
}
