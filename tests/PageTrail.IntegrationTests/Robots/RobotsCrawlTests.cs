using Microsoft.EntityFrameworkCore;
using PageTrail.Domain.Jobs;
using PageTrail.Domain.Pages;
using PageTrail.IntegrationTests.Crawling;

namespace PageTrail.IntegrationTests.Robots;

[Collection(ApiCollection.Name)]
public sealed class RobotsCrawlTests(PageTrailApiFactory factory)
{
    private const string Root = "https://fixture.test/";
    private const string Team = "https://fixture.test/team";

    private readonly CrawlHarness _harness = new(factory);

    [Fact]
    public async Task DisallowedChildPageIsSkippedWithoutBeingRequested()
    {
        _harness.Site.RobotsTxt = "User-agent: *\nDisallow: /team";
        var (jobId, _) = await _harness.SeedJobAsync(Root, maxDepth: 2);

        await _harness.CrawlUntilDoneAsync(jobId);

        await using var db = _harness.CreateDb();
        var pages = await db.Pages.Where(p => p.JobId == jobId).ToDictionaryAsync(p => p.Url, p => p.Status);
        Assert.Equal(CrawlPageStatus.Skipped, pages[Team]);
        Assert.Equal(CrawlPageStatus.Done, pages["https://fixture.test/about"]);
        Assert.Equal(0, _harness.Site.RequestsFor(Team));
        Assert.Equal(CrawlJobStatus.Completed, (await db.Jobs.SingleAsync(j => j.Id == jobId)).Status);
    }

    [Fact]
    public async Task RobotsTxtIsRequestedOncePerOriginForAJobWithSeveralPages()
    {
        _harness.Site.RobotsTxt = "User-agent: *\nDisallow: /team";
        var (jobId, _) = await _harness.SeedJobAsync(Root, maxDepth: 2);

        await _harness.CrawlUntilDoneAsync(jobId);

        Assert.Equal(1, _harness.Site.RequestsFor("https://fixture.test/robots.txt"));
        Assert.Equal(1, _harness.Site.RequestsFor("https://www.fixture.test/robots.txt"));
    }

    [Fact]
    public async Task DisallowedRootPageIsSkippedAndTheJobCompletes()
    {
        _harness.Site.RobotsTxt = "User-agent: PageTrailBot\nDisallow: /";
        var (jobId, rootId) = await _harness.SeedJobAsync(Root);

        await _harness.CrawlUntilDoneAsync(jobId);

        await using var db = _harness.CreateDb();
        Assert.Equal(CrawlPageStatus.Skipped, (await db.Pages.SingleAsync(p => p.Id == rootId)).Status);
        Assert.Equal(CrawlJobStatus.Completed, (await db.Jobs.SingleAsync(j => j.Id == jobId)).Status);
        Assert.Equal(0, _harness.Site.RequestsFor(Root));
    }
}
