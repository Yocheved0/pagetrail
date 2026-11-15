using PageTrail.Domain.Links;

namespace PageTrail.UnitTests.Links;

public sealed class PageLinksTests
{
    private static readonly Uri PageUrl = new("https://example.com/docs/");
    private static readonly StartingDomain Domain = new(new Uri("https://example.com/"));

    [Fact]
    public void RatioIsSameDomainLinksOverAllOutgoingLinks()
    {
        var links = PageLinks.From(PageUrl, ["/a", "/b", "https://www.example.com/c", "https://other.org/"], Domain);

        Assert.Equal(0.75, links.DomainLinkRatio);
    }

    [Fact]
    public void RatioIsZeroWhenThePageHasNoOutgoingLinks()
    {
        var links = PageLinks.From(PageUrl, [], Domain);

        Assert.Empty(links.Outgoing);
        Assert.Equal(0, links.DomainLinkRatio);
    }

    [Fact]
    public void IgnoredSchemesDoNotCountTowardsTheRatio()
    {
        var links = PageLinks.From(PageUrl, ["mailto:a@example.com", "tel:123", "https://other.org/"], Domain);

        Assert.Equal([new Uri("https://other.org/")], links.Outgoing);
        Assert.Equal(0, links.DomainLinkRatio);
    }

    [Fact]
    public void LinksThatNormalizeToTheSameUrlCountOnce()
    {
        var links = PageLinks.From(PageUrl, ["/a", "/a#top", "HTTPS://EXAMPLE.COM:443/a", "https://other.org/"], Domain);

        Assert.Equal([new Uri("https://example.com/a"), new Uri("https://other.org/")], links.Outgoing);
        Assert.Equal(0.5, links.DomainLinkRatio);
    }

    [Fact]
    public void LinksToThePageItselfAreNotOutgoing()
    {
        var links = PageLinks.From(PageUrl, ["#section", "/docs/", "https://other.org/"], Domain);

        Assert.Equal([new Uri("https://other.org/")], links.Outgoing);
        Assert.Equal(0, links.DomainLinkRatio);
    }

    [Fact]
    public void AllSameDomainLinksGiveRatioOne()
    {
        var links = PageLinks.From(PageUrl, ["/a", "b"], Domain);

        Assert.Equal(1, links.DomainLinkRatio);
    }

    [Fact]
    public void LinksLongerThan2048CharactersAreIgnored()
    {
        const string Prefix = "https://example.com/";
        var atLimit = Prefix + new string('a', 2048 - Prefix.Length);
        var overLimit = atLimit + "b";

        var links = PageLinks.From(PageUrl, [atLimit, overLimit, "https://other.org/"], Domain);

        Assert.Equal([new Uri(atLimit), new Uri("https://other.org/")], links.Outgoing);
        Assert.Equal(0.5, links.DomainLinkRatio);
    }
}
