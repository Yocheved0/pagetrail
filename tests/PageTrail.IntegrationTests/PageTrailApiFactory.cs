using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using PageTrail.Api.Dispatching;

namespace PageTrail.IntegrationTests;

public sealed class PageTrailApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        TestHost.UseTestDatabaseWithoutBroker(builder);
        builder.ConfigureTestServices(services => services.Remove(services.Single(d => d.ImplementationType == typeof(PageDispatcher))));
    }
}
