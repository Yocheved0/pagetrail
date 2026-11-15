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
        await ApplyFetchResultAsync(job, page, fetched, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        await CompleteJobWhenNothingQueuedAsync(job, cancellationToken);
        logger.LogInformation("Page {PageId} processed with outcome {Outcome}", page.Id, fetched.Outcome);
    }

    private async Task ApplyFetchResultAsync(CrawlJob job, CrawlPage page, FetchResult fetched, CancellationToken cancellationToken)
    {
        var domain = new StartingDomain(new Uri(job.Url));
        if (fetched.Outcome == FetchOutcome.Failed)
        {
            FailPage(job, page, fetched.FailureReason);
        }
        else if (fetched.Outcome == FetchOutcome.NotHtml || !domain.Contains(fetched.FinalUrl))
        {
            page.Skip();
        }
        else
        {
            var links = PageLinks.From(fetched.FinalUrl, LinkExtractor.ExtractHrefs(fetched.Html!), domain);
            db.Links.AddRange(page.Complete(links));
            await QueueChildrenAsync(job, page, links.Outgoing.Where(domain.Contains), cancellationToken);
        }
    }

    private void FailPage(CrawlJob job, CrawlPage page, string? reason)
    {
        page.Fail();
        if (page.IsRoot)
        {
            job.Fail($"Root page could not be fetched: {reason}", time.GetUtcNow());
        }
    }

    private async Task QueueChildrenAsync(CrawlJob job, CrawlPage page, IEnumerable<Uri> sameDomainLinks, CancellationToken cancellationToken)
    {
        if (page.Depth >= job.MaxDepth)
        {
            return;
        }

        var candidates = sameDomainLinks.DistinctBy(UrlNormalizer.DedupKey).ToList();
        var candidateKeys = candidates.Select(UrlNormalizer.DedupKey).ToList();
        var knownKeys = await db.Pages
            .Where(p => p.JobId == job.Id && candidateKeys.Contains(p.UrlKey))
            .Select(p => p.UrlKey)
            .ToListAsync(cancellationToken);
        var room = job.MaxPages - await db.Pages.CountAsync(p => p.JobId == job.Id, cancellationToken);
        var children = candidates
            .Where(url => !knownKeys.Contains(UrlNormalizer.DedupKey(url)))
            .Take(Math.Max(room, 0))
            .Select(page.Child);
        db.Pages.AddRange(children);
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
