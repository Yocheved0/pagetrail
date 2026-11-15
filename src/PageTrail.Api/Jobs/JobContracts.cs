using PageTrail.Domain.Jobs;

namespace PageTrail.Api.Jobs;

public sealed record CreateJobRequest(string? Url, int? MaxDepth);

public sealed record CreateJobResponse(Guid JobId);

public sealed record JobProgress(int Queued, int Done, int Skipped, int Failed);

public sealed record JobResponse(
    Guid Id,
    string Url,
    int MaxDepth,
    CrawlJobStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? FailureReason,
    JobProgress Progress);
