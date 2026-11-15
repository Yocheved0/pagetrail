using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PageTrail.Api.Dispatching;
using PageTrail.Infrastructure.Persistence;

namespace PageTrail.IntegrationTests;

public sealed class PageTrailApiFactory : WebApplicationFactory<Program>
{
    private const string TestDatabase = "PageTrail_IntegrationTests";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RabbitMq:HostName"] = "unused-in-api-tests",
            ["RabbitMq:UserName"] = "unused",
            ["RabbitMq:Password"] = "unused",
        }));
        builder.ConfigureTestServices(services =>
        {
            services.Remove(services.Single(d => d.ImplementationType == typeof(PageDispatcher)));
            services.Remove(services.Single(d => d.ServiceType == typeof(DbContextOptions<PageTrailDbContext>)));
            services.AddDbContext<PageTrailDbContext>((provider, options) =>
                options.UseSqlServer(TestConnectionString(provider.GetRequiredService<IConfiguration>())));
        });
    }

    private static string TestConnectionString(IConfiguration configuration) =>
        new SqlConnectionStringBuilder(configuration.GetConnectionString("Default")) { InitialCatalog = TestDatabase }.ConnectionString;
}
