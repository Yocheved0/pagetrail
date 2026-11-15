using System.Net;

namespace PageTrail.IntegrationTests;

public sealed class HealthTests(PageTrailApiFactory factory) : IClassFixture<PageTrailApiFactory>
{
    [Fact]
    public async Task HealthReturnsOkWhenDatabaseIsReachable()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
