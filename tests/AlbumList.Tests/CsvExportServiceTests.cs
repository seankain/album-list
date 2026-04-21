using AlbumList.Data;
using AlbumList.Models;
using AlbumList.Services;

namespace AlbumList.Tests;

public class CsvExportServiceTests
{
    private static AlbumDatabase CreateDatabase() => new AlbumDatabase(":memory:");

    [Fact]
    public async Task ExportAsync_WritesHeaderRow()
    {
        var db = CreateDatabase();
        var saver = new FakeFileSaver();
        var svc = new CsvExportService(db, saver);

        await svc.ExportAsync(CancellationToken.None);

        var firstLine = saver.SavedText!.Split('\n')[0].Trim();
        Assert.Equal("Name,Artist,ReleaseDate,PersonalRating,CriticalRating,Summary", firstLine);
    }

    [Fact]
    public async Task ExportAsync_SavesAsAlbumsCsv()
    {
        var db = CreateDatabase();
        var saver = new FakeFileSaver();
        var svc = new CsvExportService(db, saver);

        await svc.ExportAsync(CancellationToken.None);

        Assert.Equal("albums.csv", saver.SavedFileName);
    }

    [Fact]
    public async Task ExportAsync_FieldWithComma_QuotesField()
    {
        var db = CreateDatabase();
        await db.UpsertAsync(new Album { Name = "Songs, Ohia", Artist = "Jason Molina", Summary = "Great, amazing" });
        var saver = new FakeFileSaver();
        var svc = new CsvExportService(db, saver);

        await svc.ExportAsync(CancellationToken.None);

        Assert.Contains("\"Songs, Ohia\"", saver.SavedText!);
        Assert.Contains("\"Great, amazing\"", saver.SavedText!);
    }

    [Fact]
    public async Task ExportAsync_FieldWithQuote_DoublesInternalQuote()
    {
        var db = CreateDatabase();
        await db.UpsertAsync(new Album { Name = "The Album", Artist = "The \"Beatles\"" });
        var saver = new FakeFileSaver();
        var svc = new CsvExportService(db, saver);

        await svc.ExportAsync(CancellationToken.None);

        Assert.Contains("\"The \"\"Beatles\"\"\"", saver.SavedText!);
    }

    [Fact]
    public async Task ExportAsync_FieldWithNewline_QuotesField()
    {
        var db = CreateDatabase();
        await db.UpsertAsync(new Album { Name = "Newline Album", Artist = "Test Artist", Summary = "Line1\nLine2" });
        var saver = new FakeFileSaver();
        var svc = new CsvExportService(db, saver);

        await svc.ExportAsync(CancellationToken.None);

        Assert.Contains("\"Line1\nLine2\"", saver.SavedText!);
    }

    [Fact]
    public async Task ExportAsync_FieldWithCarriageReturn_QuotesField()
    {
        var db = CreateDatabase();
        await db.UpsertAsync(new Album { Name = "CR Album", Artist = "Test Artist", Summary = "Line1\rLine2" });
        var saver = new FakeFileSaver();
        var svc = new CsvExportService(db, saver);

        await svc.ExportAsync(CancellationToken.None);

        Assert.Contains("\"Line1\rLine2\"", saver.SavedText!);
    }

    private class FakeFileSaver : IFileSaver
    {
        public string? SavedText { get; private set; }
        public string? SavedFileName { get; private set; }

        public async Task SaveAsync(string fileName, Stream stream, CancellationToken ct)
        {
            SavedFileName = fileName;
            using var reader = new StreamReader(stream, leaveOpen: true);
            SavedText = await reader.ReadToEndAsync(ct);
        }
    }
}
