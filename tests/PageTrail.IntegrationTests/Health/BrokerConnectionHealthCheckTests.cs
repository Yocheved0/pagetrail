using Microsoft.Extensions.Diagnostics.HealthChecks;
using PageTrail.Worker.Health;

namespace PageTrail.IntegrationTests.Health;

public sealed class BrokerConnectionHealthCheckTests
{
    [Fact]
    public async Task WorkerIsUnhealthyWhenNoBrokerConnectionIsOpen()
    {
        var check = new BrokerConnectionHealthCheck(new BrokerConnectionState());

        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }
}
