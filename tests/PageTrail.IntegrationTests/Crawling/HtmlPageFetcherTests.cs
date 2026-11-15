using System.IO.Pipelines;
using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using PageTrail.Worker.Fetching;

namespace PageTrail.IntegrationTests.Crawling;

public sealed class HtmlPageFetcherTests
{
    private static readonly Uri PageUrl = new("https://fixture.test/page");
    private static readonly Uri Origin = new("https://fixture.test/");

    [Fact]
    public async Task APageThatTimesOutInTheResiliencePipelineIsFailedAsTimedOut()
    {
        await using var services = PipelineServices(new AttemptCounter(), new UnresponsiveSite());

        var result = await services.GetRequiredService<HtmlPageFetcher>().FetchAsync(PageUrl, CancellationToken.None);

        Assert.Equal(FetchOutcome.Failed, result.Outcome);
        Assert.Equal(HtmlPageFetcher.TimedOutReason, result.FailureReason);
    }

    [Fact]
    public async Task ARobotsTxtThatTimesOutInTheResiliencePipelineIsTreatedAsUnreachable()
    {
        await using var services = PipelineServices(new AttemptCounter(), new UnresponsiveSite());

        Assert.Null(await services.GetRequiredService<HtmlPageFetcher>().FetchRobotsAsync(Origin, CancellationToken.None));
    }

    [Fact]
    public async Task ABlockedAddressFailsWithoutRetries()
    {
        var attempts = new AttemptCounter();
        await using var services = PipelineServices(attempts);

        var result = await services.GetRequiredService<HtmlPageFetcher>().FetchAsync(new Uri("http://127.0.0.1:9/"), CancellationToken.None);

        Assert.Equal(PrivateAddressGuard.BlockedReason, result.FailureReason);
        Assert.Equal(1, attempts.Count);
    }

    [Fact]
    public async Task APageBodyOfExactly5MBIsRead()
    {
        var fetcher = FetcherFor(_ => new ByteArrayContent(new byte[HtmlPageFetcher.MaxBodyBytes]));

        var result = await fetcher.FetchAsync(PageUrl, CancellationToken.None);

        Assert.Equal(FetchOutcome.Html, result.Outcome);
        Assert.Equal(HtmlPageFetcher.MaxBodyBytes, result.Html!.Length);
    }

    [Fact]
    public async Task APageBodyLargerThan5MBIsFailedWithAReason()
    {
        var fetcher = FetcherFor(_ => new ByteArrayContent(new byte[HtmlPageFetcher.MaxBodyBytes + 1]));

        var result = await fetcher.FetchAsync(PageUrl, CancellationToken.None);

        Assert.Equal(FetchOutcome.Failed, result.Outcome);
        Assert.Contains("5 MB", result.FailureReason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APageBodyNotReadWithin30SecondsIsFailedWithAReason()
    {
        var fetcher = FetcherFor(_ => new StalledContent());

        var result = await fetcher.FetchAsync(PageUrl, CancellationToken.None);

        Assert.Equal(FetchOutcome.Failed, result.Outcome);
        Assert.Contains("30 s", result.FailureReason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APageBodyThatBreaksOffIsFailedWithAReason()
    {
        var fetcher = FetcherFor(_ => new BrokenContent());

        var result = await fetcher.FetchAsync(PageUrl, CancellationToken.None);

        Assert.Equal(FetchOutcome.Failed, result.Outcome);
        Assert.Equal(nameof(HttpRequestError.ResponseEnded), result.FailureReason);
    }

    [Fact]
    public async Task ARobotsTxtLargerThan5MBIsTreatedAsUnreachable()
    {
        var fetcher = FetcherFor(_ => new ByteArrayContent(new byte[HtmlPageFetcher.MaxBodyBytes + 1]));

        Assert.Null(await fetcher.FetchRobotsAsync(Origin, CancellationToken.None));
    }

    [Fact]
    public async Task ARobotsTxtNotReadWithin30SecondsIsTreatedAsUnreachable()
    {
        var fetcher = FetcherFor(_ => new StalledContent());

        Assert.Null(await fetcher.FetchRobotsAsync(Origin, CancellationToken.None));
    }

    private static HtmlPageFetcher FetcherFor(Func<HttpRequestMessage, HttpContent> content) =>
        new(new HttpClient(new StubSite(content)), new FastForwardedTime());

    private static ServiceProvider PipelineServices(AttemptCounter attempts, HttpMessageHandler? site = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        var client = services.AddPageFetcher().AddHttpMessageHandler(() => attempts);
        if (site is not null)
        {
            client.ConfigurePrimaryHttpMessageHandler(() => site);
        }

        services.PostConfigureAll<HttpStandardResilienceOptions>(options =>
        {
            options.AttemptTimeout.Timeout = TimeSpan.FromMilliseconds(100);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromMilliseconds(300);
        });
        return services.BuildServiceProvider();
    }

    private sealed class UnresponsiveSite : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class StubSite(Func<HttpRequestMessage, HttpContent> content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = content(request);
            body.Headers.ContentType = new MediaTypeHeaderValue("text/html");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = body, RequestMessage = request });
        }
    }

    private sealed class StalledContent() : StreamContent(new Pipe().Reader.AsStream());

    private sealed class BrokenContent() : StreamContent(BrokenStream())
    {
        private static Stream BrokenStream()
        {
            var pipe = new Pipe();
            pipe.Writer.Complete(new HttpIOException(HttpRequestError.ResponseEnded));
            return pipe.Reader.AsStream();
        }
    }

    private sealed class AttemptCounter : DelegatingHandler
    {
        public int Count { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Count++;
            return base.SendAsync(request, cancellationToken);
        }
    }

    private sealed class FastForwardedTime : TimeProvider
    {
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            System.CreateTimer(callback, state, dueTime == Timeout.InfiniteTimeSpan ? dueTime : dueTime / 1000, period);
    }
}
