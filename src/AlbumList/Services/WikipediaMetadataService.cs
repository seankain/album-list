using System.Text.Json;
using System.Text.RegularExpressions;

namespace AlbumList.Services;

public class WikipediaMetadataService : IMetadataService
{
    private readonly HttpClient _http;
    private static readonly Regex YearRegex = new(@"\b(19|20)\d{2}\b");

    public WikipediaMetadataService(HttpClient http) => _http = http;

    public async Task<AlbumMetadata?> LookupAsync(string name, string artist, CancellationToken ct)
    {
        try
        {
            var title = await SearchAsync(name, artist, ct);
            if (title is null) return null;

            var year = await GetYearAsync(title, ct);
            return new AlbumMetadata(year);
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> SearchAsync(string name, string artist, CancellationToken ct)
    {
        var query = Uri.EscapeDataString($"{name} {artist} album");
        var url = $"https://en.wikipedia.org/w/api.php?action=query&list=search&srsearch={query}&format=json";
        var response = await _http.GetStringAsync(url, ct);

        using var doc = JsonDocument.Parse(response);
        var hits = doc.RootElement.GetProperty("query").GetProperty("search");
        if (hits.GetArrayLength() == 0) return null;

        return hits[0].GetProperty("title").GetString();
    }

    private async Task<DateTime?> GetYearAsync(string title, CancellationToken ct)
    {
        var encoded = Uri.EscapeDataString(title);
        var url = $"https://en.wikipedia.org/api/rest_v1/page/summary/{encoded}";
        var response = await _http.GetStringAsync(url, ct);

        using var doc = JsonDocument.Parse(response);
        var extract = doc.RootElement.GetProperty("extract").GetString() ?? string.Empty;

        var match = YearRegex.Match(extract);
        if (!match.Success) return null;

        return new DateTime(int.Parse(match.Value), 1, 1);
    }
}
