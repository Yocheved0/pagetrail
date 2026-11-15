using Microsoft.EntityFrameworkCore;
using PageTrail.Domain.Jobs;
using PageTrail.Domain.Links;
using PageTrail.Domain.Pages;
using PageTrail.Infrastructure.Messaging;
using PageTrail.Infrastructure.Persistence;
using PageTrail.Worker.Fetching;
using PageTrail.Worker.Robots;

namespace PageTrail.Worker.Processing;

public sealed class CrawlPageProcessor(
    PageTrailDbContext db,
    HtmlPageFetcher fetcher,
    RobotsPolicy robots,
    TimeProvider time,
    ILogger<CrawlPageProcessor> logger)
{
    public async Task<ProcessingResult> ProcessAsync(CrawlPageMessage message, CancellationToken cancellationToken)
    {
        var page = await FindPageAsync(message, cancellationToken);
        var job = await db.Jobs.AsNoTracking().SingleOrDefaultAsync(j => j.Id == message.JobId, cancellationToken);
        if (page is null || job is null)
        {
            return ProcessingResult.UnknownPage;
        }

        if (page.Status != CrawlPageStatus.Queued || job.IsFinished)
        {
            logger.LogInformation("Page {PageId} skipped: already processed or job finished", page.Id);
            return ProcessingResult.AlreadyProcessed;
        }

        var fetched = await FetchAllowedAsync(page, cancellationToken);
        var outcome = ApplyFetchResult(new StartingDomain(new Uri(job.Url)), page, fetched);
        if (!await CommitAsync(page, outcome, cancellationToken))
        {
            logger.LogInformation("Page {PageId} discarded: already claimed by another delivery", page.Id);
            return ProcessingResult.AlreadyProcessed;
        }

        logger.LogInformation("Page {PageId} processed with outcome {Outcome}", page.Id, fetched.Outcome);
        return ProcessingResult.Committed;
    }

    public async Task MarkFailedAsync(CrawlPageMessage message, string reason, CancellationToken cancellationToken)
    {
        // WHY: a failed attempt can leave its unsaved links and child pages tracked on this context
        db.ChangeTracker.Clear();
        var page = await FindPageAsync(message, cancellationToken);
        if (page is null)
        {
            return;
        }

        page.Fail();
        if (await CommitAsync(page, PageOutcome.WithoutLinks(reason), cancellationToken))
        {
            logger.LogWarning("Page {PageId} marked failed after its final attempt", page.Id);
        }
    }

    private Task<CrawlPage?> FindPageAsync(CrawlPageMessage message, CancellationToken cancellationToken) =>
        db.Pages.AsNoTracking().SingleOrDefaultAsync(p => p.Id == message.PageId && p.JobId == message.JobId, cancellationToken);

    private async Task<FetchResult> FetchAllowedAsync(CrawlPage page, CancellationToken cancellationToken)
    {
        var url = new Uri(page.Url);
        return await robots.AllowsAsync(page.JobId, url, cancellationToken)
            ? await fetcher.FetchAsync(url, cancellationToken)
            : new FetchResult(FetchOutcome.Disallowed, url);
    }

    private static PageOutcome ApplyFetchResult(StartingDomain domain, CrawlPage page, FetchResult fetched)
    {
        if (fetched.Outcome == FetchOutcome.Failed)
        {
            page.Fail();
            return PageOutcome.WithoutLinks($"Root page could not be fetched: {fetched.FailureReason}");
        }

        if (fetched.Outcome is FetchOutcome.NotHtml or FetchOutcome.Disallowed || !domain.Contains(fetched.FinalUrl))
        {
            page.Skip();
            return PageOutcome.WithoutLinks();
        }

        var links = PageLinks.From(fetched.FinalUrl, LinkExtractor.ExtractHrefs(fetched.Html!), domain);
        return new PageOutcome(page.Complete(links), links.Outgoing.Where(domain.Contains).ToList(), null);
    }

    private async Task<bool> CommitAsync(CrawlPage page, PageOutcome outcome, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var job = await LockJobAsync(page.JobId, cancellationToken);
        if (job.IsFinished || !await ClaimAsync(page, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        job.Start(time.GetUtcNow());
        db.Links.AddRange(outcome.Links);
        await QueueChildrenAsync(job, page, outcome.ChildCandidates, cancellationToken);
        if (page.Status == CrawlPageStatus.Failed && page.IsRoot)
        {
            job.Fail(outcome.FailureReason ?? "Root page failed", time.GetUtcNow());
        }

        await db.SaveChangesAsync(cancellationToken);
        await CompleteJobWhenNothingQueuedAsync(job, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private Task<CrawlJob> LockJobAsync(Guid jobId, CancellationToken cancellationToken) =>
        db.Jobs.FromSql($"SELECT * FROM Jobs WITH (UPDLOCK, ROWLOCK) WHERE Id = {jobId}").SingleAsync(cancellationToken);

    private async Task<bool> ClaimAsync(CrawlPage page, CancellationToken cancellationToken)
    {
        var claimed = await db.Pages
            .Where(p => p.Id == page.Id && p.Status == CrawlPageStatus.Queued)
            .ExecuteUpdateAsync(
                set => set.SetProperty(p => p.Status, page.Status).SetProperty(p => p.DomainLinkRatio, page.DomainLinkRatio),
                cancellationToken);
        return claimed == 1;
    }

    private async Task QueueChildrenAsync(CrawlJob job, CrawlPage page, IReadOnlyList<Uri> sameDomainLinks, CancellationToken cancellationToken)
    {
        if (sameDomainLinks.Count == 0 || page.Depth >= job.MaxDepth)
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
