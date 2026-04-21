using AlbumList.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AlbumList.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IExportService _csvExportService;
    private readonly DatabaseExportService _databaseExportService;

    public SettingsViewModel(IExportService csvExportService, DatabaseExportService databaseExportService)
    {
        _csvExportService = csvExportService;
        _databaseExportService = databaseExportService;
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
}
