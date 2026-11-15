using System.Net;
using System.Net.Http.Headers;

namespace PageTrail.IntegrationTests.Crawling;

public sealed class FixtureSiteHandler : HttpMessageHandler
{
    private static readonly string FixtureRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures", "site");

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath.Trim('/');
        var file = Path.Combine(FixtureRoot, path.Length == 0 ? "index.html" : path);
        if (!File.Exists(file))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = request });
        }

        var content = new ByteArrayContent(File.ReadAllBytes(file));
        content.Headers.ContentType = new MediaTypeHeaderValue(file.EndsWith(".html", StringComparison.Ordinal) ? "text/html" : "image/png");
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content, RequestMessage = request });
    }
}
