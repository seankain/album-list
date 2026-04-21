using AlbumList.Data;

namespace AlbumList.Services;

public class DatabaseExportService : IExportService
{
    private readonly AlbumDatabase _db;
    private readonly IFileSaver _fileSaver;

    public DatabaseExportService(AlbumDatabase db, IFileSaver fileSaver)
    {
        _db = db;
        _fileSaver = fileSaver;
    }

    public async Task ExportAsync(CancellationToken ct)
    {
        using var stream = File.OpenRead(_db.DbPath);
        await _fileSaver.SaveAsync("albums.db3", stream, ct);
    }
}
