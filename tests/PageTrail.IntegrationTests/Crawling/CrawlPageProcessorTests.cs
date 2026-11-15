using Microsoft.EntityFrameworkCore;
using PageTrail.Domain.Jobs;
using PageTrail.Domain.Pages;

namespace PageTrail.IntegrationTests.Crawling;

[Collection(ApiCollection.Name)]
public sealed class CrawlPageProcessorTests(PageTrailApiFactory factory)
{
    private readonly CrawlHarness _harness = new(factory);

    [Fact]
    public async Task RootPageIsStoredWithItsDistinctOutgoingLinksAndRatio()
    {
        var (jobId, pageId) = await _harness.SeedJobAsync("https://fixture.test/", maxDepth: 0);

        await _harness.ProcessAsync(jobId, pageId);

        await using var db = _harness.CreateDb();
        var page = await db.Pages.SingleAsync(p => p.Id == pageId);
        Assert.Equal(CrawlPageStatus.Done, page.Status);
        Assert.Equal(2.0 / 3, page.DomainLinkRatio!.Value, 6);
        var links = await db.Links.Where(l => l.SourcePageId == pageId).Select(l => l.TargetUrl).OrderBy(u => u).ToListAsync();
        Assert.Equal(["https://external.example/", "https://fixture.test/about", "https://www.fixture.test/contact"], links);
        var job = await db.Jobs.SingleAsync(j => j.Id == jobId);
        Assert.Equal(CrawlJobStatus.Completed, job.Status);
        Assert.NotNull(job.StartedAt);
        Assert.NotNull(job.CompletedAt);
    }

    [Fact]
    public async Task NonHtmlRootPageIsSkippedAndTheJobCompletes()
    {
        var (jobId, pageId) = await _harness.SeedJobAsync("https://fixture.test/logo.png");

        await _harness.ProcessAsync(jobId, pageId);

        await using var db = _harness.CreateDb();
        Assert.Equal(CrawlPageStatus.Skipped, (await db.Pages.SingleAsync(p => p.Id == pageId)).Status);
        Assert.False(await db.Links.AnyAsync(l => l.SourcePageId == pageId));
        Assert.Equal(CrawlJobStatus.Completed, (await db.Jobs.SingleAsync(j => j.Id == jobId)).Status);
    }

    [Fact]
    public async Task UnreachableRootPageFailsTheJobWithAReason()
    {
        var (jobId, pageId) = await _harness.SeedJobAsync("https://fixture.test/missing");

        await _harness.ProcessAsync(jobId, pageId);

        await using var db = _harness.CreateDb();
        Assert.Equal(CrawlPageStatus.Failed, (await db.Pages.SingleAsync(p => p.Id == pageId)).Status);
        var job = await db.Jobs.SingleAsync(j => j.Id == jobId);
        Assert.Equal(CrawlJobStatus.Failed, job.Status);
        Assert.Contains("404", job.FailureReason, StringComparison.Ordinal);
    }
}
