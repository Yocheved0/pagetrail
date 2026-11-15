using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;

namespace PageTrail.IntegrationTests.Crawling;

public sealed class FixtureSiteHandler : HttpMessageHandler
{
    private const string LeavesDomainPath = "leaves";
    private static readonly Uri OffSiteLanding = new("https://elsewhere.example/landing");
    private static readonly string FixtureRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures", "site");

    private readonly ConcurrentDictionary<string, int> _requests = new();

    public int RequestsFor(string url) => _requests.GetValueOrDefault(url);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _requests.AddOrUpdate(request.RequestUri!.AbsoluteUri, 1, (_, count) => count + 1);
        var path = request.RequestUri.AbsolutePath.Trim('/');
        var file = FindFile(path);
        if (file is null)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = request });
        }

        var content = new ByteArrayContent(File.ReadAllBytes(file));
        content.Headers.ContentType = new MediaTypeHeaderValue(file.EndsWith(".html", StringComparison.Ordinal) ? "text/html" : "image/png");
        var finalRequest = path == LeavesDomainPath ? new HttpRequestMessage(HttpMethod.Get, OffSiteLanding) : request;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content, RequestMessage = finalRequest });
    }

    private static string? FindFile(string path)
    {
        var candidates = path.Length == 0 ? ["index.html"] : new[] { path + ".html", path };
        return candidates.Select(name => Path.Combine(FixtureRoot, name)).FirstOrDefault(File.Exists);
    }
}
