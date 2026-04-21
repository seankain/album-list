namespace AlbumList.Services;

public record AlbumMetadata(DateTime? ReleaseDate);

public interface IMetadataService
{
    Task<AlbumMetadata?> LookupAsync(string name, string artist, CancellationToken ct);
}
