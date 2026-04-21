namespace AlbumList.Services;

public interface IExportService
{
    Task ExportAsync(CancellationToken ct);
}
