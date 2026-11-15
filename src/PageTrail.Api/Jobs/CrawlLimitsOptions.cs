using System.ComponentModel.DataAnnotations;

namespace PageTrail.Api.Jobs;

public sealed class CrawlLimitsOptions
{
    public const string SectionName = "Crawl";

    public const int MinDepth = 0;

    public const int MaxDepth = 5;

    public const int DefaultDepth = 2;

    [Range(1, 10_000)]
    public int MaxPagesPerJob { get; init; } = 200;
}
