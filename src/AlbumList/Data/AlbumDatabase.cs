using AlbumList.Models;
using SQLite;

namespace AlbumList.Data;

public class AlbumDatabase
{
    private SQLiteAsyncConnection? _connection;
    private readonly string _dbPath;

    public AlbumDatabase(string? dbPath = null)
    {
#if ANDROID
        _dbPath = dbPath ?? Path.Combine(FileSystem.AppDataDirectory, "albums.db3");
#else
        _dbPath = dbPath ?? ":memory:";
#endif
    }

    private async Task<SQLiteAsyncConnection> GetConnectionAsync()
    {
        if (_connection is null)
        {
            _connection = new SQLiteAsyncConnection(_dbPath);
            await _connection.CreateTableAsync<Album>();
            await _connection.ExecuteAsync(
                "CREATE UNIQUE INDEX IF NOT EXISTS ux_album_name_artist " +
                "ON Album (Name COLLATE NOCASE, Artist COLLATE NOCASE)");
        }
        return _connection;
    }

    public async Task<List<Album>> GetAllAsync(AlbumSort sort)
    {
        var conn = await GetConnectionAsync();
        return sort switch
        {
            AlbumSort.Artist => await conn.QueryAsync<Album>(
                "SELECT * FROM Album ORDER BY Artist COLLATE NOCASE, Name COLLATE NOCASE"),
            AlbumSort.Year => await conn.QueryAsync<Album>(
                "SELECT * FROM Album ORDER BY CASE WHEN ReleaseDate IS NULL THEN 1 ELSE 0 END, ReleaseDate, Name COLLATE NOCASE"),
            _ => await conn.QueryAsync<Album>(
                "SELECT * FROM Album ORDER BY Name COLLATE NOCASE"),
        };
    }

    public async Task<Album?> GetAsync(int id)
    {
        var conn = await GetConnectionAsync();
        return await conn.FindAsync<Album>(id);
    }

    public async Task<Album?> FindDuplicateAsync(string name, string artist)
    {
        var conn = await GetConnectionAsync();
        var results = await conn.QueryAsync<Album>(
            "SELECT * FROM Album WHERE Name = ? COLLATE NOCASE AND Artist = ? COLLATE NOCASE LIMIT 1",
            name, artist);
        return results.FirstOrDefault();
    }

    public async Task<int> UpsertAsync(Album album)
    {
        var conn = await GetConnectionAsync();
        if (album.Id == 0)
            return await conn.InsertAsync(album);
        return await conn.UpdateAsync(album);
    }

    public async Task<int> DeleteAsync(int id)
    {
        var conn = await GetConnectionAsync();
        return await conn.DeleteAsync<Album>(id);
    }

    public async Task<List<Album>> GetIncompleteAsync()
    {
        var conn = await GetConnectionAsync();
        return await conn.QueryAsync<Album>("SELECT * FROM Album WHERE ReleaseDate IS NULL");
    }
}
