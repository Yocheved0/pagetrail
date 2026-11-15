using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using PageTrail.Api.Jobs;
using PageTrail.Domain.Jobs;
using PageTrail.Domain.Pages;

namespace PageTrail.IntegrationTests.Crawling;

[Collection(ApiCollection.Name)]
public sealed class SiteCrawlTests(PageTrailApiFactory factory)
{
    private const string Root = "https://fixture.test/";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly CrawlHarness _harness = new(factory);

    [Fact]
    public async Task SameDomainLinksAreCrawledOnceEachUpToMaxDepth()
    {
        var (jobId, _) = await _harness.SeedJobAsync(Root, maxDepth: 2);

        await _harness.CrawlUntilDoneAsync(jobId);

        var pages = await PagesOfAsync(jobId);
        Assert.Equal(
            new Dictionary<string, CrawlPageStatus>
            {
                [Root] = CrawlPageStatus.Done,
                ["https://fixture.test/about"] = CrawlPageStatus.Done,
                ["https://www.fixture.test/contact"] = CrawlPageStatus.Done,
                ["https://fixture.test/team"] = CrawlPageStatus.Done,
                ["https://fixture.test/logo.png"] = CrawlPageStatus.Skipped,
                ["https://fixture.test/leaves"] = CrawlPageStatus.Skipped,
            },
            pages);
        Assert.All(pages.Keys, url => Assert.Equal(1, _harness.Site.RequestsFor(url)));
        Assert.Equal(0, _harness.Site.RequestsFor("https://external.example/"));
        Assert.Equal(0, _harness.Site.RequestsFor("https://fixture.test/deep"));
        await using var db = _harness.CreateDb();
        Assert.Equal(CrawlJobStatus.Completed, (await db.Jobs.SingleAsync(j => j.Id == jobId)).Status);
    }

    [Fact]
    public async Task MaxDepthOneStopsAtTheRootsChildren()
    {
        var (jobId, _) = await _harness.SeedJobAsync(Root, maxDepth: 1);

        await _harness.CrawlUntilDoneAsync(jobId);

        Assert.Equal([Root, "https://fixture.test/about", "https://www.fixture.test/contact"], (await PagesOfAsync(jobId)).Keys.Order());
    }

    [Fact]
    public async Task NoPagesAreQueuedBeyondTheMaxPagesLimit()
    {
        var (jobId, _) = await _harness.SeedJobAsync(Root, maxDepth: 2, maxPages: 2);

        await _harness.CrawlUntilDoneAsync(jobId);

        Assert.Equal([Root, "https://fixture.test/about"], (await PagesOfAsync(jobId)).Keys.Order());
    }

    [Fact]
    public async Task TreeNestsPagesUnderTheirFirstDiscovererWithRatiosAndLinks()
    {
        var (jobId, _) = await _harness.SeedJobAsync(Root, maxDepth: 2);
        await _harness.CrawlUntilDoneAsync(jobId);

        var tree = await factory.CreateClient().GetFromJsonAsync<PageNode>($"/api/jobs/{jobId}/tree", Json);

        Assert.NotNull(tree);
        Assert.Equal(Root, tree.Url);
        Assert.Equal(2.0 / 3, tree.DomainLinkRatio!.Value, 6);
        Assert.Contains("https://external.example/", tree.Links);
        Assert.Equal(["https://fixture.test/about", "https://www.fixture.test/contact"], tree.Children.Select(c => c.Url));
        var about = tree.Children[0];
        Assert.Equal(
            ["https://fixture.test/team", "https://fixture.test/logo.png", "https://fixture.test/leaves"],
            about.Children.Select(c => c.Url));
        Assert.Equal(CrawlPageStatus.Skipped, about.Children[1].Status);
    }

    [Fact]
    public async Task TreeOfAnUnknownJobReturns404()
    {
        var response = await factory.CreateClient().GetAsync(new Uri($"/api/jobs/{Guid.NewGuid()}/tree", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<Dictionary<string, CrawlPageStatus>> PagesOfAsync(Guid jobId)
    {
        await using var db = _harness.CreateDb();
        return await db.Pages.Where(p => p.JobId == jobId).ToDictionaryAsync(p => p.Url, p => p.Status);
    }
}
