using Microsoft.EntityFrameworkCore;
using PageTrail.Domain.Jobs;
using PageTrail.Domain.Pages;
using PageTrail.Worker.Processing;

namespace PageTrail.IntegrationTests.Crawling;

[Collection(ApiCollection.Name)]
public sealed class ReliableDeliveryTests(PageTrailApiFactory factory)
{
    private const string Root = "https://fixture.test/";

    private readonly CrawlHarness _harness = new(factory);

    [Fact]
    public async Task SameMessageDeliveredTwiceLeavesPageAndLinkRowsUnchanged()
    {
        var (jobId, pageId) = await _harness.SeedJobAsync(Root, maxDepth: 1);
        var body = CrawlHarness.BodyOf(jobId, pageId);

        Assert.Equal(DeliveryDecision.Ack, await _harness.HandleAsync(body, attempt: 1));
        var afterFirst = await CountRowsAsync(jobId);
        Assert.Equal(DeliveryDecision.Ack, await _harness.HandleAsync(body, attempt: 1));

        Assert.Equal(afterFirst, await CountRowsAsync(jobId));
        Assert.Equal((3, 3), afterFirst);
    }

    [Fact]
    public async Task StaleDeliveryWhosePageWasProcessedDuringTheFetchWritesNothing()
    {
        var (jobId, pageId) = await _harness.SeedJobAsync(Root, maxDepth: 1);
        var raced = false;
        _harness.Site.BeforeRespond = async _ =>
        {
            if (!raced)
            {
                raced = true;
                await _harness.ProcessAsync(jobId, pageId);
            }
        };

        await _harness.ProcessAsync(jobId, pageId);

        Assert.True(raced);
        Assert.Equal((3, 3), await CountRowsAsync(jobId));
    }

    [Fact]
    public async Task UnparseableBodyIsDeadLettered()
    {
        Assert.Equal(DeliveryDecision.DeadLetter, await _harness.HandleAsync("not json"u8.ToArray(), attempt: 1));
    }

    [Fact]
    public async Task MessageForAnUnknownPageIsDeadLettered()
    {
        var (jobId, _) = await _harness.SeedJobAsync(Root);

        Assert.Equal(DeliveryDecision.DeadLetter, await _harness.HandleAsync(CrawlHarness.BodyOf(jobId, long.MaxValue), attempt: 1));
    }

    [Fact]
    public async Task HandlerFailureIsRetriedWithTheNextAttemptUntilTheLast()
    {
        var (jobId, pageId) = await _harness.SeedJobAsync(Root);
        _harness.Site.BeforeRespond = _ => throw new InvalidOperationException("fetch blew up");
        var body = CrawlHarness.BodyOf(jobId, pageId);

        Assert.Equal(DeliveryDecision.Retry(2), await _harness.HandleAsync(body, attempt: 1));
        Assert.Equal(DeliveryDecision.Retry(3), await _harness.HandleAsync(body, attempt: 2));

        await using var db = _harness.CreateDb();
        Assert.Equal(CrawlPageStatus.Queued, (await db.Pages.SingleAsync(p => p.Id == pageId)).Status);
    }

    [Fact]
    public async Task RootPageFailingOnTheLastAttemptIsFailedThenDeadLetteredAndFailsTheJob()
    {
        var (jobId, pageId) = await _harness.SeedJobAsync(Root);
        _harness.Site.BeforeRespond = _ => throw new InvalidOperationException("fetch blew up");

        var decision = await _harness.HandleAsync(CrawlHarness.BodyOf(jobId, pageId), attempt: CrawlDeliveryHandler.MaxAttempts);

        Assert.Equal(DeliveryDecision.DeadLetter, decision);
        await using var db = _harness.CreateDb();
        Assert.Equal(CrawlPageStatus.Failed, (await db.Pages.SingleAsync(p => p.Id == pageId)).Status);
        var job = await db.Jobs.SingleAsync(j => j.Id == jobId);
        Assert.Equal(CrawlJobStatus.Failed, job.Status);
        Assert.False(string.IsNullOrEmpty(job.FailureReason));
    }

    [Fact]
    public async Task ChildPageFailingOnTheLastAttemptDoesNotFailTheJob()
    {
        var (jobId, rootId) = await _harness.SeedJobAsync(Root, maxDepth: 1);
        await _harness.ProcessAsync(jobId, rootId);
        var childId = await ChildIdAsync(jobId, "https://fixture.test/about");
        _harness.Site.BeforeRespond = _ => throw new InvalidOperationException("fetch blew up");

        var decision = await _harness.HandleAsync(CrawlHarness.BodyOf(jobId, childId), attempt: CrawlDeliveryHandler.MaxAttempts);

        Assert.Equal(DeliveryDecision.DeadLetter, decision);
        await using var db = _harness.CreateDb();
        Assert.Equal(CrawlPageStatus.Failed, (await db.Pages.SingleAsync(p => p.Id == childId)).Status);
        Assert.Equal(CrawlJobStatus.Running, (await db.Jobs.SingleAsync(j => j.Id == jobId)).Status);
    }

    private async Task<long> ChildIdAsync(Guid jobId, string url)
    {
        await using var db = _harness.CreateDb();
        return await db.Pages.Where(p => p.JobId == jobId && p.Url == url).Select(p => p.Id).SingleAsync();
    }

    private async Task<(int Pages, int Links)> CountRowsAsync(Guid jobId)
    {
        await using var db = _harness.CreateDb();
        var pages = await db.Pages.CountAsync(p => p.JobId == jobId);
        var links = await db.Links.CountAsync(l => db.Pages.Any(p => p.Id == l.SourcePageId && p.JobId == jobId));
        return (pages, links);
    }
}
