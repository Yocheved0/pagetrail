using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using PageTrail.Api.Jobs;
using PageTrail.Domain.Jobs;
using PageTrail.Domain.Pages;
using PageTrail.IntegrationTests.Crawling;

namespace PageTrail.IntegrationTests.Jobs;

[Collection(ApiCollection.Name)]
public sealed class CancelJobTests(PageTrailApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _client = factory.CreateClient();
    private readonly CrawlHarness _harness = new(factory);

    [Fact]
    public async Task CancelingAnActiveJobReturnsItAsCanceled()
    {
        var (jobId, _) = await _harness.SeedJobAsync("https://fixture.test/");

        var response = await _client.PostAsync(new Uri($"/api/jobs/{jobId}/cancel", UriKind.Relative), null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var job = await response.Content.ReadFromJsonAsync<JobResponse>(Json);
        Assert.Equal(CrawlJobStatus.Canceled, job!.Status);
        Assert.NotNull(job.CompletedAt);
        Assert.Equal(1, job.Progress.Queued);
    }

    [Fact]
    public async Task TheWorkerSkipsPagesOfACanceledJob()
    {
        var (jobId, pageId) = await _harness.SeedJobAsync("https://fixture.test/");
        await _client.PostAsync(new Uri($"/api/jobs/{jobId}/cancel", UriKind.Relative), null);

        await _harness.ProcessAsync(jobId, pageId);

        await using var db = _harness.CreateDb();
        Assert.Equal(CrawlPageStatus.Queued, (await db.Pages.SingleAsync(p => p.Id == pageId)).Status);
        Assert.False(await db.Links.AnyAsync(l => l.SourcePageId == pageId));
        Assert.Equal(0, _harness.Site.RequestsFor("https://fixture.test/"));
    }

    [Fact]
    public async Task CancelingAFinishedJobReturns409ProblemDetails()
    {
        var (jobId, _) = await _harness.SeedJobAsync("https://fixture.test/", maxDepth: 0);
        await _harness.CrawlUntilDoneAsync(jobId);

        var response = await _client.PostAsync(new Uri($"/api/jobs/{jobId}/cancel", UriKind.Relative), null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task CancelingAnUnknownJobReturns404()
    {
        var response = await _client.PostAsync(new Uri($"/api/jobs/{Guid.NewGuid()}/cancel", UriKind.Relative), null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
