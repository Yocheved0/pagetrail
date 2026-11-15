using PageTrail.Domain.Jobs;
using PageTrail.Domain.Links;

namespace PageTrail.Domain.Pages;

public sealed class CrawlPage
{
    private CrawlPage(Guid jobId, string url, int depth, long? parentPageId)
    {
        JobId = jobId;
        Url = url;
        Depth = depth;
        ParentPageId = parentPageId;
    }

    public long Id { get; private set; }

    public Guid JobId { get; private set; }

    public string Url { get; private set; }

    public int Depth { get; private set; }

    public long? ParentPageId { get; private set; }

    public CrawlPageStatus Status { get; private set; } = CrawlPageStatus.Queued;

    public double? DomainLinkRatio { get; private set; }

    public bool IsPublished { get; private set; }

    public bool IsRoot => ParentPageId is null;

    public static CrawlPage Root(CrawlJob job) => new(job.Id, job.Url, depth: 0, parentPageId: null);

    public void MarkPublished() => IsPublished = true;

    public IReadOnlyList<PageLink> Complete(PageLinks links)
    {
        Status = CrawlPageStatus.Done;
        DomainLinkRatio = links.DomainLinkRatio;
        return links.Outgoing.Select(target => new PageLink(Id, target.AbsoluteUri)).ToList();
    }

    public void Skip() => Status = CrawlPageStatus.Skipped;

    public void Fail() => Status = CrawlPageStatus.Failed;
}
