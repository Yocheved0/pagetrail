using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PageTrail.Infrastructure.Persistence;

namespace PageTrail.IntegrationTests;

public static class TestHost
{
    private const string TestDatabase = "PageTrail_IntegrationTests";

    public static void UseTestDatabaseWithoutBroker(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RabbitMq:HostName"] = "unused-in-tests",
            ["RabbitMq:UserName"] = "unused",
            ["RabbitMq:Password"] = "unused",
        }));
        builder.ConfigureTestServices(services =>
        {
            services.Remove(services.Single(d => d.ServiceType == typeof(DbContextOptions<PageTrailDbContext>)));
            services.AddDbContext<PageTrailDbContext>((provider, options) =>
                options.UseSqlServer(TestConnectionString(provider.GetRequiredService<IConfiguration>())));
        });
    }

    private static string TestConnectionString(IConfiguration configuration) =>
        new SqlConnectionStringBuilder(configuration.GetConnectionString("Default")) { InitialCatalog = TestDatabase }.ConnectionString;
}
