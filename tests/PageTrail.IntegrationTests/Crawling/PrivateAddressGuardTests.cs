using System.Net;
using PageTrail.Worker.Fetching;

namespace PageTrail.IntegrationTests.Crawling;

public sealed class PrivateAddressGuardTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")]
    [InlineData("0.0.0.0")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    [InlineData("fd00::1")]
    [InlineData("::ffff:10.0.0.1")]
    public void LocalAndPrivateAddressesAreBlocked(string address)
    {
        Assert.True(PrivateAddressGuard.IsBlocked(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("93.184.215.14")]
    [InlineData("172.32.0.1")]
    [InlineData("8.8.8.8")]
    [InlineData("2606:4700:4700::1111")]
    public void PublicAddressesAreAllowed(string address)
    {
        Assert.False(PrivateAddressGuard.IsBlocked(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("http://127.0.0.1:9/")]
    [InlineData("http://localhost:9/")]
    public async Task FetchingALocalAddressFailsWithAClearReason(string url)
    {
        using var http = new HttpClient(PrivateAddressGuard.CreateHandler());

        var result = await new HtmlPageFetcher(http).FetchAsync(new Uri(url), CancellationToken.None);

        Assert.Equal(FetchOutcome.Failed, result.Outcome);
        Assert.Equal(PrivateAddressGuard.BlockedReason, result.FailureReason);
    }
}
