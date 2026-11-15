namespace PageTrail.Domain.Pages;

public sealed class PageLink(long sourcePageId, string targetUrl)
{
    public long Id { get; private set; }

    public long SourcePageId { get; private set; } = sourcePageId;

    public string TargetUrl { get; private set; } = targetUrl;
}
