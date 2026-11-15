using Microsoft.EntityFrameworkCore;
using PageTrail.Domain.Pages;
using PageTrail.Infrastructure.Persistence;

namespace PageTrail.Api.Jobs;

public sealed record PageNode(
    long Id,
    string Url,
    int Depth,
    CrawlPageStatus Status,
    double? DomainLinkRatio,
    IReadOnlyList<string> Links,
    List<PageNode> Children);

public static class PageTree
{
    public static async Task<PageNode?> LoadAsync(PageTrailDbContext db, Guid jobId, CancellationToken cancellationToken)
    {
        var pages = await db.Pages.AsNoTracking().Where(p => p.JobId == jobId).OrderBy(p => p.Id).ToListAsync(cancellationToken);
        var links = await db.Links.AsNoTracking()
            .Where(l => db.Pages.Any(p => p.Id == l.SourcePageId && p.JobId == jobId))
            .OrderBy(l => l.Id)
            .ToListAsync(cancellationToken);
        var linksByPage = links.ToLookup(l => l.SourcePageId, l => l.TargetUrl);
        var nodes = pages.ToDictionary(p => p.Id, p => new PageNode(p.Id, p.Url, p.Depth, p.Status, p.DomainLinkRatio, linksByPage[p.Id].ToList(), []));

        PageNode? root = null;
        foreach (var page in pages)
        {
            if (page.ParentPageId is { } parentId)
            {
                nodes[parentId].Children.Add(nodes[page.Id]);
            }
            else
            {
                root = nodes[page.Id];
            }
        }

        return root;
    }
}
