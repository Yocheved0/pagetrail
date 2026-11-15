using PageTrail.Domain.Links;

namespace PageTrail.UnitTests.Links;

public sealed class UrlNormalizerTests
{
    private static readonly Uri PageUrl = new("https://example.com/docs/guide/");

    [Theory]
    [InlineData("intro", "https://example.com/docs/guide/intro")]
    [InlineData("../api", "https://example.com/docs/api")]
    [InlineData("/about", "https://example.com/about")]
    [InlineData("//cdn.example.org/lib", "https://cdn.example.org/lib")]
    [InlineData("?page=2", "https://example.com/docs/guide/?page=2")]
    public void RelativeLinksResolveAgainstThePageUrl(string href, string expected)
    {
        Assert.Equal(new Uri(expected), UrlNormalizer.Normalize(PageUrl, href));
    }

    [Theory]
    [InlineData("intro#setup", "https://example.com/docs/guide/intro")]
    [InlineData("#top", "https://example.com/docs/guide/")]
    [InlineData("https://example.com/a?x=1#frag", "https://example.com/a?x=1")]
    public void FragmentsAreDropped(string href, string expected)
    {
        Assert.Equal(expected, UrlNormalizer.Normalize(PageUrl, href)!.AbsoluteUri);
    }

    [Theory]
    [InlineData("HTTPS://EXAMPLE.COM/Path?Q=A", "https://example.com/Path?Q=A")]
    [InlineData("https://example.com:443/a", "https://example.com/a")]
    [InlineData("http://example.com:80/a", "http://example.com/a")]
    [InlineData("http://example.com:8080/a", "http://example.com:8080/a")]
    [InlineData("https://example.com", "https://example.com/")]
    public void SchemeHostAndDefaultPortAreNormalizedWhilePathAndQueryKeepTheirCase(string href, string expected)
    {
        Assert.Equal(expected, UrlNormalizer.Normalize(PageUrl, href)!.AbsoluteUri);
    }

    [Theory]
    [InlineData("mailto:someone@example.com")]
    [InlineData("tel:+15550100")]
    [InlineData("javascript:void(0)")]
    [InlineData("ftp://example.com/file")]
    [InlineData("data:text/plain,hi")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("http://")]
    public void NonHttpOrUnusableLinksAreIgnored(string href)
    {
        Assert.Null(UrlNormalizer.Normalize(PageUrl, href));
    }

    [Fact]
    public void SurroundingWhitespaceIsTrimmed()
    {
        Assert.Equal(new Uri("https://example.com/docs/guide/intro"), UrlNormalizer.Normalize(PageUrl, "  intro \n"));
    }
}
