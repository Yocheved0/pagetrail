using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace PageTrail.IntegrationTests;

public sealed class PageTrailApiFactory : WebApplicationFactory<Program>
{
    private const string TestDatabase = "PageTrail_IntegrationTests";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var connection = new SqlConnectionStringBuilder(config.Build().GetConnectionString("Default"))
            {
                InitialCatalog = TestDatabase,
            };
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = connection.ConnectionString,
            });
        });
    }
}
