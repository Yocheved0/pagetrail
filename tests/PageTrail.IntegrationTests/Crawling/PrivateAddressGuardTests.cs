using System.Net;
using System.Net.Sockets;
using System.Text;
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
    [InlineData("0.1.2.3")]
    [InlineData("100.64.0.1")]
    [InlineData("100.127.255.255")]
    [InlineData("192.0.0.8")]
    [InlineData("198.18.0.1")]
    [InlineData("198.19.255.255")]
    [InlineData("224.0.0.1")]
    [InlineData("239.255.255.250")]
    [InlineData("240.0.0.1")]
    [InlineData("255.255.255.255")]
    public void SharedBenchmarkingMulticastAndReservedIPv4RangesAreBlocked(string address)
    {
        Assert.True(PrivateAddressGuard.IsBlocked(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("64:ff9b::a00:1")]
    [InlineData("64:ff9b::7f00:1")]
    [InlineData("2002:a00:1::1")]
    [InlineData("2002:a9fe:a9fe::")]
    [InlineData("::10.0.0.1")]
    [InlineData("::127.0.0.1")]
    public void IPv6AddressesEmbeddingABlockedIPv4AddressAreBlocked(string address)
    {
        Assert.True(PrivateAddressGuard.IsBlocked(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("93.184.215.14")]
    [InlineData("172.32.0.1")]
    [InlineData("8.8.8.8")]
    [InlineData("2606:4700:4700::1111")]
    [InlineData("100.63.255.255")]
    [InlineData("100.128.0.1")]
    [InlineData("198.20.0.1")]
    [InlineData("223.255.255.255")]
    [InlineData("64:ff9b::808:808")]
    [InlineData("2002:808:808::1")]
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

        var result = await new HtmlPageFetcher(http, TimeProvider.System).FetchAsync(new Uri(url), CancellationToken.None);

        Assert.Equal(FetchOutcome.Failed, result.Outcome);
        Assert.Equal(PrivateAddressGuard.BlockedReason, result.FailureReason);
    }

    [Fact]
    public void TheHandlerBypassesProxiesSoTheGuardSeesTheRealTarget()
    {
        using var handler = PrivateAddressGuard.CreateHandler();

        Assert.False(handler.UseProxy);
    }

    [Fact]
    public async Task FollowingARedirectToABlockedAddressFailsWithAClearReason()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var blockedTarget = IPAddress.Parse("127.0.0.2");
        var server = RedirectOnceAsync(listener, new Uri($"http://{blockedTarget}:{port}/"));
        using var http = new HttpClient(PrivateAddressGuard.CreateHandler(address => address.Equals(blockedTarget)));

        var result = await new HtmlPageFetcher(http, TimeProvider.System).FetchAsync(new Uri($"http://127.0.0.1:{port}/"), CancellationToken.None);

        Assert.Equal(FetchOutcome.Failed, result.Outcome);
        Assert.Equal(PrivateAddressGuard.BlockedReason, result.FailureReason);
        await server.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private static async Task RedirectOnceAsync(TcpListener listener, Uri location)
    {
        using var client = await listener.AcceptTcpClientAsync();
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        while (!string.IsNullOrEmpty(await reader.ReadLineAsync()))
        {
        }

        var response = $"HTTP/1.1 302 Found\r\nLocation: {location}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(response));
    }
}
