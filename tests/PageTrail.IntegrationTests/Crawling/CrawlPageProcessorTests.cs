using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PageTrail.Domain.Jobs;
using PageTrail.Domain.Pages;
using PageTrail.Infrastructure.Messaging;
using PageTrail.Infrastructure.Persistence;
using PageTrail.Worker.Fetching;
using PageTrail.Worker.Processing;

namespace PageTrail.IntegrationTests.Crawling;

[Collection(ApiCollection.Name)]
public sealed class CrawlPageProcessorTests(PageTrailApiFactory factory)
{
    [Fact]
    public async Task RootPageIsStoredWithItsDistinctOutgoingLinksAndRatio()
    {
        var (jobId, pageId) = await SeedJobAsync("https://fixture.test/");

        await ProcessAsync(jobId, pageId);

        await using var db = CreateDb();
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
        var (jobId, pageId) = await SeedJobAsync("https://fixture.test/logo.png");

        await ProcessAsync(jobId, pageId);

        await using var db = CreateDb();
        Assert.Equal(CrawlPageStatus.Skipped, (await db.Pages.SingleAsync(p => p.Id == pageId)).Status);
        Assert.False(await db.Links.AnyAsync(l => l.SourcePageId == pageId));
        Assert.Equal(CrawlJobStatus.Completed, (await db.Jobs.SingleAsync(j => j.Id == jobId)).Status);
    }

    [Fact]
    public async Task UnreachableRootPageFailsTheJobWithAReason()
    {
        var (jobId, pageId) = await SeedJobAsync("https://fixture.test/missing");

        await ProcessAsync(jobId, pageId);

        await using var db = CreateDb();
        Assert.Equal(CrawlPageStatus.Failed, (await db.Pages.SingleAsync(p => p.Id == pageId)).Status);
        var job = await db.Jobs.SingleAsync(j => j.Id == jobId);
        Assert.Equal(CrawlJobStatus.Failed, job.Status);
        Assert.Contains("404", job.FailureReason, StringComparison.Ordinal);
    }

    private PageTrailDbContext CreateDb() =>
        factory.Services.CreateScope().ServiceProvider.GetRequiredService<PageTrailDbContext>();

    private async Task<(Guid JobId, long PageId)> SeedJobAsync(string url)
    {
        await using var db = CreateDb();
        var job = CrawlJob.Create(new Uri(url), maxDepth: 2, maxPages: 200, DateTimeOffset.UtcNow);
        var root = CrawlPage.Root(job);
        db.Jobs.Add(job);
        db.Pages.Add(root);
        await db.SaveChangesAsync();
        return (job.Id, root.Id);
    }

    private async Task ProcessAsync(Guid jobId, long pageId)
    {
        await using var db = CreateDb();
        using var http = new HttpClient(new FixtureSiteHandler());
        var processor = new CrawlPageProcessor(db, new HtmlPageFetcher(http), TimeProvider.System, NullLogger<CrawlPageProcessor>.Instance);

        await processor.ProcessAsync(new CrawlPageMessage(jobId, pageId), CancellationToken.None);
    }
}
