using System.Net;
using System.Text;
using AlbumList.Services;

namespace AlbumList.Tests;

public class WikipediaMetadataServiceTests
{
    private static WikipediaMetadataService BuildService(params string[] jsonResponses)
    {
        var handler = new FakeHttpMessageHandler(jsonResponses);
        return new WikipediaMetadataService(new HttpClient(handler));
    }

    [Fact]
    public async Task LookupAsync_HappyPath_ReturnsYear()
    {
        var searchJson = """{"query":{"search":[{"title":"The Dark Side of the Moon"}]}}""";
        var summaryJson = """{"extract":"The Dark Side of the Moon is a 1973 studio album by Pink Floyd."}""";

        var svc = BuildService(searchJson, summaryJson);
        var result = await svc.LookupAsync("The Dark Side of the Moon", "Pink Floyd", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(1973, result!.ReleaseDate!.Value.Year);
    }

    [Fact]
    public async Task LookupAsync_NoSearchHits_ReturnsNull()
    {
        var searchJson = """{"query":{"search":[]}}""";

        var svc = BuildService(searchJson);
        var result = await svc.LookupAsync("Unknown Album", "Unknown Artist", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task LookupAsync_MalformedExtract_ReturnsNullReleaseDate()
    {
        var searchJson = """{"query":{"search":[{"title":"Some Album"}]}}""";
        var summaryJson = """{"extract":"This article has no year information whatsoever."}""";

        var svc = BuildService(searchJson, summaryJson);
        var result = await svc.LookupAsync("Some Album", "Some Artist", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Null(result!.ReleaseDate);
    }
}

public class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<string> _responses;

    public FakeHttpMessageHandler(params string[] jsonResponses)
    {
        _responses = new Queue<string>(jsonResponses);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (_responses.Count == 0)
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(_responses.Dequeue(), Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
    }
}
