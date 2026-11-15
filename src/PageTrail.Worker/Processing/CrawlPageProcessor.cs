using Microsoft.EntityFrameworkCore;
using PageTrail.Domain.Jobs;
using PageTrail.Domain.Links;
using PageTrail.Domain.Pages;
using PageTrail.Infrastructure.Messaging;
using PageTrail.Infrastructure.Persistence;
using PageTrail.Worker.Fetching;

namespace PageTrail.Worker.Processing;

public sealed class CrawlPageProcessor(
    PageTrailDbContext db,
    HtmlPageFetcher fetcher,
    TimeProvider time,
    ILogger<CrawlPageProcessor> logger)
{
    public async Task ProcessAsync(CrawlPageMessage message, CancellationToken cancellationToken)
    {
        var page = await db.Pages.SingleOrDefaultAsync(p => p.Id == message.PageId && p.JobId == message.JobId, cancellationToken);
        var job = await db.Jobs.SingleOrDefaultAsync(j => j.Id == message.JobId, cancellationToken);
        if (page is null || job is null || page.Status != CrawlPageStatus.Queued || job.IsFinished)
        {
            logger.LogInformation("Page {PageId} skipped: already processed or job finished", message.PageId);
            return;
        }

        job.Start(time.GetUtcNow());
        var fetched = await fetcher.FetchAsync(new Uri(page.Url), cancellationToken);
        ApplyFetchResult(job, page, fetched);
        await db.SaveChangesAsync(cancellationToken);

        await CompleteJobWhenNothingQueuedAsync(job, cancellationToken);
        logger.LogInformation("Page {PageId} processed with outcome {Outcome}", page.Id, fetched.Outcome);
    }

    private void ApplyFetchResult(CrawlJob job, CrawlPage page, FetchResult fetched)
    {
        switch (fetched.Outcome)
        {
            case FetchOutcome.Html:
                var links = PageLinks.From(new Uri(page.Url), LinkExtractor.ExtractHrefs(fetched.Html!), new StartingDomain(new Uri(job.Url)));
                db.Links.AddRange(page.Complete(links));
                break;
            case FetchOutcome.NotHtml:
                page.Skip();
                break;
            default:
                page.Fail();
                if (page.IsRoot)
                {
                    job.Fail($"Root page could not be fetched: {fetched.FailureReason}", time.GetUtcNow());
                }

                break;
        }
    }

    private async Task CompleteJobWhenNothingQueuedAsync(CrawlJob job, CancellationToken cancellationToken)
    {
        if (job.IsFinished || await db.Pages.AnyAsync(p => p.JobId == job.Id && p.Status == CrawlPageStatus.Queued, cancellationToken))
        {
            return;
        }

        job.Complete(time.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
    }
}
