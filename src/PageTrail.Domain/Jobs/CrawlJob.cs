namespace PageTrail.Domain.Jobs;

public sealed class CrawlJob
{
    private CrawlJob(Guid id, string url, int maxDepth, int maxPages, DateTimeOffset createdAt)
    {
        Id = id;
        Url = url;
        MaxDepth = maxDepth;
        MaxPages = maxPages;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string Url { get; private set; }

    public int MaxDepth { get; private set; }

    public int MaxPages { get; private set; }

    public CrawlJobStatus Status { get; private set; } = CrawlJobStatus.Pending;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public string? FailureReason { get; private set; }

    public bool IsFinished => Status is CrawlJobStatus.Completed or CrawlJobStatus.Failed or CrawlJobStatus.Canceled;

    public static CrawlJob Create(Uri url, int maxDepth, int maxPages, DateTimeOffset now) =>
        new(Guid.NewGuid(), url.AbsoluteUri, maxDepth, maxPages, now);

    public void Start(DateTimeOffset now)
    {
        if (Status == CrawlJobStatus.Pending)
        {
            Status = CrawlJobStatus.Running;
            StartedAt = now;
        }
    }

    public void Complete(DateTimeOffset now) => Finish(CrawlJobStatus.Completed, now);

    public void Fail(string reason, DateTimeOffset now)
    {
        FailureReason = reason;
        Finish(CrawlJobStatus.Failed, now);
    }

    private void Finish(CrawlJobStatus status, DateTimeOffset now)
    {
        Status = status;
        CompletedAt = now;
    }
}
