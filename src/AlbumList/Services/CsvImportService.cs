using System.Text;
using AlbumList.Data;
using AlbumList.Messages;
using AlbumList.Models;
using CommunityToolkit.Mvvm.Messaging;

namespace AlbumList.Services;

public class CsvImportService
{
    private readonly AlbumDatabase _db;
    private readonly MetadataBackgroundWorker _worker;

    public CsvImportService(AlbumDatabase db, MetadataBackgroundWorker worker)
    {
        _db = db;
        _worker = worker;
    }

    public async Task<ImportResult> ImportAsync(CancellationToken ct)
    {
        var pickResult = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Select CSV to import",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.Android, ["text/csv", "text/comma-separated-values", "application/csv", "*/*"] },
                { DevicePlatform.iOS, ["public.comma-separated-values-text"] },
                { DevicePlatform.WinUI, [".csv"] },
                { DevicePlatform.macOS, ["csv"] },
            })
        });

        if (pickResult is null)
            return new ImportResult(0, 0, 0);

        string content;
        using (var stream = await pickResult.OpenReadAsync())
        using (var reader = new StreamReader(stream, Encoding.UTF8))
            content = await reader.ReadToEndAsync(ct);

        var rows = ParseCsv(content);

        int imported = 0, merged = 0, skipped = 0;
        bool isHeader = true;

        foreach (var fields in rows)
        {
            if (ct.IsCancellationRequested)
                break;

            if (isHeader)
            {
                isHeader = false;
                continue;
            }

            var name = fields.Count > 0 ? fields[0].Trim() : "";
            var artist = fields.Count > 1 ? fields[1].Trim() : "";

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(artist))
            {
                skipped++;
                continue;
            }

            DateTime? releaseDate = null;
            if (fields.Count > 2 && !string.IsNullOrEmpty(fields[2]) &&
                DateTime.TryParseExact(fields[2], "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var parsed))
            {
                releaseDate = parsed;
            }

            int.TryParse(fields.Count > 3 ? fields[3] : "", out int personalRating);
            int.TryParse(fields.Count > 4 ? fields[4] : "", out int criticalRating);
            var summary = fields.Count > 5 ? fields[5] : "";

            var existing = await _db.FindDuplicateAsync(name, artist);
            if (existing is not null)
            {
                bool changed = false;
                if (!existing.ReleaseDate.HasValue && releaseDate.HasValue)
                {
                    existing.ReleaseDate = releaseDate;
                    changed = true;
                }
                if (existing.PersonalRating == 0 && personalRating != 0)
                {
                    existing.PersonalRating = personalRating;
                    changed = true;
                }
                if (existing.CriticalRating == 0 && criticalRating != 0)
                {
                    existing.CriticalRating = criticalRating;
                    changed = true;
                }
                if (string.IsNullOrEmpty(existing.Summary) && !string.IsNullOrEmpty(summary))
                {
                    existing.Summary = summary;
                    changed = true;
                }

                if (changed)
                {
                    await _db.UpsertAsync(existing);
                    merged++;
                }
                else
                {
                    skipped++;
                }
            }
            else
            {
                var album = new Album
                {
                    Name = name,
                    Artist = artist,
                    ReleaseDate = releaseDate,
                    PersonalRating = personalRating,
                    CriticalRating = criticalRating,
                    Summary = string.IsNullOrEmpty(summary) ? null : summary,
                };
                await _db.UpsertAsync(album);
                if (!album.ReleaseDate.HasValue)
                    _worker.Enqueue(album.Id);
                imported++;
            }
        }

        if (imported > 0 || merged > 0)
            WeakReferenceMessenger.Default.Send(new AlbumsUpdatedMessage());

        return new ImportResult(imported, merged, skipped);
    }

    // RFC 4180-compliant parser that handles quoted multiline fields.
    private static List<List<string>> ParseCsv(string content)
    {
        var rows = new List<List<string>>();
        var fields = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;
        int i = 0;

        while (i < content.Length)
        {
            char c = content[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < content.Length && content[i + 1] == '"')
                    {
                        current.Append('"');
                        i += 2;
                    }
                    else
                    {
                        inQuotes = false;
                        i++;
                    }
                }
                else
                {
                    current.Append(c);
                    i++;
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                    i++;
                }
                else if (c == ',')
                {
                    fields.Add(current.ToString());
                    current.Clear();
                    i++;
                }
                else if (c == '\r' || c == '\n')
                {
                    fields.Add(current.ToString());
                    current.Clear();
                    if (fields.Any(f => !string.IsNullOrEmpty(f)))
                        rows.Add(fields);
                    fields = [];
                    if (c == '\r' && i + 1 < content.Length && content[i + 1] == '\n')
                        i++;
                    i++;
                }
                else
                {
                    current.Append(c);
                    i++;
                }
            }
        }

        // Flush last field/row
        fields.Add(current.ToString());
        if (fields.Any(f => !string.IsNullOrEmpty(f)))
            rows.Add(fields);

        return rows;
    }
}

public record ImportResult(int Imported, int Merged, int Skipped);
