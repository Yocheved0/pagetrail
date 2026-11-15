using System.Net;

namespace PageTrail.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class HealthTests(PageTrailApiFactory factory)
{
    [Fact]
    public async Task HealthReturnsOkWhenDatabaseIsReachable()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
