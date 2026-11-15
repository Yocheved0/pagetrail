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

public sealed class CrawlHarness(PageTrailApiFactory factory)
{
    public FixtureSiteHandler Site { get; } = new();

    public PageTrailDbContext CreateDb() =>
        factory.Services.CreateScope().ServiceProvider.GetRequiredService<PageTrailDbContext>();

    public async Task<(Guid JobId, long PageId)> SeedJobAsync(string url, int maxDepth = 2, int maxPages = 200)
    {
        await using var db = CreateDb();
        var job = CrawlJob.Create(new Uri(url), maxDepth, maxPages, DateTimeOffset.UtcNow);
        var root = CrawlPage.Root(job);
        db.Jobs.Add(job);
        db.Pages.Add(root);
        await db.SaveChangesAsync();
        return (job.Id, root.Id);
    }

    public async Task ProcessAsync(Guid jobId, long pageId)
    {
        await using var db = CreateDb();
        using var http = new HttpClient(Site, disposeHandler: false);
        var processor = new CrawlPageProcessor(db, new HtmlPageFetcher(http), TimeProvider.System, NullLogger<CrawlPageProcessor>.Instance);

        await processor.ProcessAsync(new CrawlPageMessage(jobId, pageId), CancellationToken.None);
    }

    public async Task CrawlUntilDoneAsync(Guid jobId)
    {
        while (await NextQueuedPageAsync(jobId) is { } pageId)
        {
            await ProcessAsync(jobId, pageId);
        }
    }

    private async Task<long?> NextQueuedPageAsync(Guid jobId)
    {
        await using var db = CreateDb();
        return await db.Pages
            .Where(p => p.JobId == jobId && p.Status == CrawlPageStatus.Queued)
            .OrderBy(p => p.Id)
            .Select(p => (long?)p.Id)
            .FirstOrDefaultAsync();
    }
}
