namespace PageTrail.Domain.Jobs;

public enum CrawlJobStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Canceled,
}
