namespace AlbumList.Services;

public class MauiFileSaverAdapter : IFileSaver
{
    private readonly CommunityToolkit.Maui.Storage.IFileSaver _inner;

    public MauiFileSaverAdapter(CommunityToolkit.Maui.Storage.IFileSaver inner)
        => _inner = inner;

    public async Task SaveAsync(string fileName, Stream stream, CancellationToken ct)
        => await _inner.SaveAsync(fileName, stream, ct);
}
