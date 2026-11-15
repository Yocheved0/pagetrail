using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;

namespace PageTrail.IntegrationTests.Crawling;

public sealed class FixtureSiteHandler : HttpMessageHandler
{
    private const string LeavesDomainPath = "leaves";
    private const string RobotsPath = "robots.txt";
    private static readonly Uri OffSiteLanding = new("https://elsewhere.example/landing");
    private static readonly string FixtureRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures", "site");

    private readonly ConcurrentDictionary<string, int> _requests = new();

    public Func<Uri, Task>? BeforeRespond { get; set; }

    public string? RobotsTxt { get; set; }

    public int RequestsFor(string url) => _requests.GetValueOrDefault(url);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _requests.AddOrUpdate(request.RequestUri!.AbsoluteUri, 1, (_, count) => count + 1);
        var path = request.RequestUri.AbsolutePath.Trim('/');
        if (path == RobotsPath)
        {
            return RobotsResponse(request);
        }

        if (BeforeRespond is { } hook)
        {
            await hook(request.RequestUri);
        }

        var file = FindFile(path);
        if (file is null)
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = request };
        }

        var content = new ByteArrayContent(await File.ReadAllBytesAsync(file, cancellationToken));
        content.Headers.ContentType = new MediaTypeHeaderValue(file.EndsWith(".html", StringComparison.Ordinal) ? "text/html" : "image/png");
        var finalRequest = path == LeavesDomainPath ? new HttpRequestMessage(HttpMethod.Get, OffSiteLanding) : request;
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content, RequestMessage = finalRequest };
    }

    private HttpResponseMessage RobotsResponse(HttpRequestMessage request) =>
        RobotsTxt is null
            ? new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = request }
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(RobotsTxt), RequestMessage = request };

    private static string? FindFile(string path)
    {
        var candidates = path.Length == 0 ? ["index.html"] : new[] { path + ".html", path };
        return candidates.Select(name => Path.Combine(FixtureRoot, name)).FirstOrDefault(File.Exists);
    }
}
