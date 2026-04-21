using AlbumList.Data;

namespace AlbumList.Services;

public class CsvExportService : IExportService
{
    private readonly AlbumDatabase _db;
    private readonly IFileSaver _fileSaver;

    public CsvExportService(AlbumDatabase db, IFileSaver fileSaver)
    {
        _db = db;
        _fileSaver = fileSaver;
    }

    public async Task ExportAsync(CancellationToken ct)
    {
        var albums = await _db.GetAllAsync(AlbumSort.Name);
        using var ms = new MemoryStream();
        await using (var writer = new StreamWriter(ms, System.Text.Encoding.UTF8, bufferSize: 4096, leaveOpen: true))
        {
            await writer.WriteLineAsync("Name,Artist,ReleaseDate,PersonalRating,CriticalRating,Summary");
            foreach (var album in albums)
            {
                await writer.WriteLineAsync(string.Join(",",
                    CsvEscape(album.Name),
                    CsvEscape(album.Artist),
                    album.ReleaseDate?.ToString("yyyy-MM-dd") ?? "",
                    album.PersonalRating.ToString(),
                    album.CriticalRating.ToString(),
                    CsvEscape(album.Summary ?? "")));
            }
        }
        ms.Position = 0;
        await _fileSaver.SaveAsync("albums.csv", ms, ct);
    }

    private static string CsvEscape(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\r') || field.Contains('\n'))
            return $"\"{field.Replace("\"", "\"\"")}\"";
        return field;
    }
}
