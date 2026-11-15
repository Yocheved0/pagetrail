using Microsoft.Extensions.Caching.Memory;
using PageTrail.Worker.Fetching;

namespace PageTrail.Worker.Robots;

public sealed class RobotsPolicy(HtmlPageFetcher fetcher, IMemoryCache cache)
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    public async Task<bool> AllowsAsync(Guid jobId, Uri url, CancellationToken cancellationToken)
    {
        var origin = new Uri(url.GetLeftPart(UriPartial.Authority));
        var rules = await cache.GetOrCreateAsync(
            new RobotsCacheKey(jobId, origin),
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheDuration;
                var content = await fetcher.FetchRobotsAsync(origin, cancellationToken);
                return content is null ? RobotsRules.AllowAll : RobotsRules.Parse(content);
            });
        return rules!.Allows(url.PathAndQuery);
    }

    private sealed record RobotsCacheKey(Guid JobId, Uri Origin);
}
