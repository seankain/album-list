namespace AlbumList.Services;

public interface IFileSaver
{
    Task SaveAsync(string fileName, Stream stream, CancellationToken ct);
}
