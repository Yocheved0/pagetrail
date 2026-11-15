using PageTrail.Domain.Links;

namespace PageTrail.UnitTests.Links;

public sealed class StartingDomainTests
{
    [Theory]
    [InlineData("https://example.com/", "https://example.com/a", true)]
    [InlineData("https://example.com/", "http://EXAMPLE.com:8080/a", true)]
    [InlineData("https://example.com/", "https://www.example.com/a", true)]
    [InlineData("https://www.example.com/", "https://example.com/a", true)]
    [InlineData("https://example.com/", "https://blog.example.com/a", false)]
    [InlineData("https://example.com/", "https://example.org/a", false)]
    [InlineData("https://example.com/", "https://notexample.com/a", false)]
    [InlineData("https://www.example.com/", "https://www.www.example.com/a", false)]
    public void ContainsComparesHostsIgnoringOneLeadingWww(string jobUrl, string url, bool expected)
    {
        var domain = new StartingDomain(new Uri(jobUrl));

        Assert.Equal(expected, domain.Contains(new Uri(url)));
    }
}
