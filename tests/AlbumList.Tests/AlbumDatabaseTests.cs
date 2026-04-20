using AlbumList.Data;
using AlbumList.Models;

namespace AlbumList.Tests;

public class AlbumDatabaseTests : IAsyncLifetime
{
    private AlbumDatabase _db = null!;

    public async Task InitializeAsync()
    {
        SQLitePCL.Batteries_V2.Init();
        _db = new AlbumDatabase(":memory:");
        // Trigger init by performing a no-op read
        await _db.GetIncompleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Insert_SetsNewId()
    {
        var album = new Album { Name = "Test Album", Artist = "Test Artist" };
        await _db.UpsertAsync(album);
        Assert.True(album.Id > 0);
    }

    [Fact]
    public async Task GetAllAsync_SortByName_ReturnsAlphabetically()
    {
        await _db.UpsertAsync(new Album { Name = "Zebra", Artist = "Artist A" });
        await _db.UpsertAsync(new Album { Name = "Apple", Artist = "Artist B" });
        var albums = await _db.GetAllAsync(AlbumSort.Name);
        Assert.Equal("Apple", albums[0].Name);
        Assert.Equal("Zebra", albums[1].Name);
    }

    [Fact]
    public async Task GetAllAsync_SortByArtist_ReturnsByArtist()
    {
        await _db.UpsertAsync(new Album { Name = "Album 1", Artist = "Zappa" });
        await _db.UpsertAsync(new Album { Name = "Album 2", Artist = "Aardvark" });
        var albums = await _db.GetAllAsync(AlbumSort.Artist);
        Assert.Equal("Aardvark", albums[0].Artist);
        Assert.Equal("Zappa", albums[1].Artist);
    }

    [Fact]
    public async Task GetAllAsync_SortByYear_ReturnsByReleaseDate()
    {
        await _db.UpsertAsync(new Album { Name = "Newer", Artist = "Artist A", ReleaseDate = new DateTime(2020, 1, 1) });
        await _db.UpsertAsync(new Album { Name = "Older", Artist = "Artist B", ReleaseDate = new DateTime(1990, 1, 1) });
        var albums = await _db.GetAllAsync(AlbumSort.Year);
        Assert.Equal("Older", albums[0].Name);
        Assert.Equal("Newer", albums[1].Name);
    }

    [Fact]
    public async Task GetAllAsync_SortByYear_NullDatesLast()
    {
        await _db.UpsertAsync(new Album { Name = "Has Date", Artist = "Artist A", ReleaseDate = new DateTime(2000, 1, 1) });
        await _db.UpsertAsync(new Album { Name = "No Date", Artist = "Artist B" });
        var albums = await _db.GetAllAsync(AlbumSort.Year);
        Assert.Equal("Has Date", albums[0].Name);
        Assert.Equal("No Date", albums[1].Name);
    }

    [Fact]
    public async Task FindDuplicateAsync_CaseInsensitive_FindsMatch()
    {
        await _db.UpsertAsync(new Album { Name = "Dark Side", Artist = "Pink Floyd" });
        var dup = await _db.FindDuplicateAsync("dark side", "pink floyd");
        Assert.NotNull(dup);
        Assert.Equal("Dark Side", dup.Name);
    }

    [Fact]
    public async Task FindDuplicateAsync_NoMatch_ReturnsNull()
    {
        var dup = await _db.FindDuplicateAsync("Nonexistent", "Nobody");
        Assert.Null(dup);
    }

    [Fact]
    public async Task DeleteAsync_RemovesAlbum()
    {
        var album = new Album { Name = "Delete Me", Artist = "Artist" };
        await _db.UpsertAsync(album);
        await _db.DeleteAsync(album.Id);
        var result = await _db.GetAsync(album.Id);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetIncompleteAsync_OnlyReturnsMissingReleaseDate()
    {
        await _db.UpsertAsync(new Album { Name = "Has Date", Artist = "Artist A", ReleaseDate = new DateTime(2000, 1, 1) });
        await _db.UpsertAsync(new Album { Name = "No Date", Artist = "Artist B" });
        var incomplete = await _db.GetIncompleteAsync();
        Assert.Single(incomplete);
        Assert.Equal("No Date", incomplete[0].Name);
    }
}
