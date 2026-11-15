using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PageTrail.Domain.Jobs;
using PageTrail.Domain.Pages;
using PageTrail.Infrastructure.Messaging;
using PageTrail.Infrastructure.Persistence;
using PageTrail.Worker.Fetching;
using PageTrail.Worker.Processing;
using PageTrail.Worker.Robots;

namespace PageTrail.IntegrationTests.Crawling;

public sealed class CrawlHarness(PageTrailApiFactory factory)
{
    private static readonly MemoryCache RobotsCache = new(new MemoryCacheOptions());

    public FixtureSiteHandler Site { get; } = new();

    public static byte[] BodyOf(Guid jobId, long pageId) => JsonSerializer.SerializeToUtf8Bytes(new CrawlPageMessage(jobId, pageId));

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

        await CreateProcessor(db, http).ProcessAsync(new CrawlPageMessage(jobId, pageId), CancellationToken.None);
    }

    public async Task<DeliveryDecision> HandleAsync(byte[] body, int attempt)
    {
        await using var db = CreateDb();
        using var http = new HttpClient(Site, disposeHandler: false);
        var handler = new CrawlDeliveryHandler(CreateProcessor(db, http), NullLogger<CrawlDeliveryHandler>.Instance);

        return await handler.HandleAsync(body, attempt, CancellationToken.None);
    }

    public async Task CrawlUntilDoneAsync(Guid jobId)
    {
        while (await NextQueuedPageAsync(jobId) is { } pageId)
        {
            await ProcessAsync(jobId, pageId);
        }
    }

    private static CrawlPageProcessor CreateProcessor(PageTrailDbContext db, HttpClient http)
    {
        var fetcher = new HtmlPageFetcher(http);
        return new(db, fetcher, new RobotsPolicy(fetcher, RobotsCache), TimeProvider.System, NullLogger<CrawlPageProcessor>.Instance);
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
