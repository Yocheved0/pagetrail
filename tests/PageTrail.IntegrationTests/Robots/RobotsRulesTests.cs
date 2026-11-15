using PageTrail.Worker.Robots;

namespace PageTrail.IntegrationTests.Robots;

public sealed class RobotsRulesTests
{
    [Fact]
    public void OurGroupIsChosenOverTheWildcardGroup()
    {
        var rules = RobotsRules.Parse("User-agent: *\nDisallow: /\n\nUser-agent: pagetrailbot\nDisallow: /private");

        Assert.True(rules.Allows("/public"));
        Assert.False(rules.Allows("/private/page"));
    }

    [Fact]
    public void WildcardGroupIsUsedWhenNoGroupNamesOurBot()
    {
        var rules = RobotsRules.Parse("User-agent: OtherBot\nDisallow: /\n\nUser-agent: *\nDisallow: /admin");

        Assert.True(rules.Allows("/home"));
        Assert.False(rules.Allows("/admin"));
    }

    [Fact]
    public void ConsecutiveUserAgentLinesShareOneGroup()
    {
        var rules = RobotsRules.Parse("User-agent: OtherBot\nUser-agent: PageTrailBot\nDisallow: /shared\n\nUser-agent: *\nDisallow: /");

        Assert.False(rules.Allows("/shared"));
        Assert.True(rules.Allows("/elsewhere"));
    }

    [Fact]
    public void LongestMatchingRuleWins()
    {
        var rules = RobotsRules.Parse("User-agent: *\nDisallow: /docs\nAllow: /docs/public");

        Assert.False(rules.Allows("/docs/secret"));
        Assert.True(rules.Allows("/docs/public/page"));
    }

    [Fact]
    public void AllowWinsATieWithDisallow()
    {
        var rules = RobotsRules.Parse("User-agent: *\nDisallow: /page\nAllow: /page");

        Assert.True(rules.Allows("/page"));
    }

    [Fact]
    public void WildcardMatchesAnySequenceOfCharacters()
    {
        var rules = RobotsRules.Parse("User-agent: *\nDisallow: /*.pdf");

        Assert.False(rules.Allows("/files/report.pdf"));
        Assert.True(rules.Allows("/files/report.html"));
    }

    [Fact]
    public void DollarAnchorsThePatternToTheEndOfThePath()
    {
        var rules = RobotsRules.Parse("User-agent: *\nDisallow: /*.pdf$");

        Assert.False(rules.Allows("/report.pdf"));
        Assert.True(rules.Allows("/report.pdf.html"));
    }

    [Fact]
    public void EmptyDisallowAllowsEverything()
    {
        var rules = RobotsRules.Parse("User-agent: *\nDisallow:");

        Assert.True(rules.Allows("/anything"));
    }

    [Fact]
    public void CommentsAndUnknownLinesAreIgnored()
    {
        var rules = RobotsRules.Parse("# site rules\nUser-agent: * # everyone\nCrawl-delay: 10\nSitemap: https://fixture.test/sitemap.xml\nDisallow: /tmp # scratch\nnonsense line");

        Assert.False(rules.Allows("/tmp/file"));
        Assert.True(rules.Allows("/home"));
    }

    [Fact]
    public void QueryStringIsPartOfTheMatchedPath()
    {
        var rules = RobotsRules.Parse("User-agent: *\nDisallow: /search?q=");

        Assert.False(rules.Allows("/search?q=cats"));
        Assert.True(rules.Allows("/search"));
    }

    [Fact]
    public void EmptyRobotsAllowsEverything()
    {
        Assert.True(RobotsRules.Parse(string.Empty).Allows("/"));
    }
}
