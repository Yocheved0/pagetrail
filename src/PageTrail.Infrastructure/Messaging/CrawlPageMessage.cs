namespace PageTrail.Infrastructure.Messaging;

public sealed record CrawlPageMessage(Guid JobId, long PageId);
