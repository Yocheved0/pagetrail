using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using PageTrail.Worker.Processing;

namespace PageTrail.IntegrationTests;

public sealed class PageTrailWorkerFactory : WebApplicationFactory<CrawlPageConsumer>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        TestHost.UseTestDatabaseWithoutBroker(builder);
        builder.ConfigureTestServices(services => services.Remove(services.Single(d => d.ImplementationType == typeof(CrawlPageConsumer))));
    }
}
