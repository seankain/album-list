using System.Threading.Channels;
using AlbumList.Data;
using AlbumList.Messages;
using AlbumList.Models;
using CommunityToolkit.Mvvm.Messaging;

namespace AlbumList.Services;

public class MetadataBackgroundWorker
{
    private readonly AlbumDatabase _database;
    private readonly IMetadataService _metadataService;
    private readonly IConnectivity _connectivity;
    private readonly Channel<int> _channel = Channel.CreateUnbounded<int>();
    private CancellationTokenSource? _cts;

    public MetadataBackgroundWorker(
        AlbumDatabase database,
        IMetadataService metadataService,
        IConnectivity connectivity)
    {
        _database = database;
        _metadataService = metadataService;
        _connectivity = connectivity;
    }

    public void Enqueue(int albumId) => _channel.Writer.TryWrite(albumId);

    public void Start()
    {
        if (_cts is not null)
            return;
        _cts = new CancellationTokenSource();
        Task.Run(() => RunLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            List<Album> albums;

            try
            {
                albums = await DrainChannelAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (albums.Count == 0 || _connectivity.NetworkAccess != NetworkAccess.Internet)
                continue;

            bool anyUpdated = false;
            int backoffMs = 2_000;

            foreach (var album in albums)
            {
                if (ct.IsCancellationRequested)
                    break;

                try
                {
                    var metadata = await _metadataService.LookupAsync(album.Name, album.Artist, ct);
                    if (metadata?.ReleaseDate.HasValue == true)
                    {
                        album.ReleaseDate = metadata.ReleaseDate;
                        await _database.UpsertAsync(album);
                        anyUpdated = true;
                        backoffMs = 2_000;
                    }
                }
                catch (Exception) when (!ct.IsCancellationRequested)
                {
                    await Task.Delay(backoffMs, ct).ConfigureAwait(false);
                    backoffMs = Math.Min(backoffMs * 2, 30_000);
                }
            }

            if (anyUpdated)
                WeakReferenceMessenger.Default.Send(new AlbumsUpdatedMessage());
        }
    }

    private async Task<List<Album>> DrainChannelAsync(CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromMinutes(5));

        List<int> ids = [];

        try
        {
            var id = await _channel.Reader.ReadAsync(timeoutCts.Token);
            ids.Add(id);
            while (_channel.Reader.TryRead(out var nextId))
                ids.Add(nextId);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // 5-minute timeout fallback: scan for all albums missing ReleaseDate
            return await _database.GetIncompleteAsync();
        }

        var albums = new List<Album>();
        foreach (var id in ids)
        {
            var album = await _database.GetAsync(id);
            if (album is not null && !album.ReleaseDate.HasValue)
                albums.Add(album);
        }
        return albums;
    }
}
