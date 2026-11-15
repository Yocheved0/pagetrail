using Microsoft.Extensions.Http.Resilience;
using PageTrail.Worker.Robots;
using Polly;

namespace PageTrail.Worker.Fetching;

public static class PageFetcherServices
{
    public static IHttpClientBuilder AddPageFetcher(this IServiceCollection services)
    {
        var client = services
            .AddHttpClient<HtmlPageFetcher>(http => http.DefaultRequestHeaders.UserAgent.ParseAdd($"{RobotsRules.UserAgentToken}/1.0"))
            .ConfigurePrimaryHttpMessageHandler(() => PrivateAddressGuard.CreateHandler());
        client.AddStandardResilienceHandler(options => options.Retry.ShouldHandle = args => ValueTask.FromResult(IsRetryable(args.Outcome)));
        return client;
    }

    private static bool IsRetryable(Outcome<HttpResponseMessage> outcome) =>
        outcome.Exception is not HttpRequestException { InnerException: BlockedAddressException }
        && HttpClientResiliencePredicates.IsTransient(outcome);
}
