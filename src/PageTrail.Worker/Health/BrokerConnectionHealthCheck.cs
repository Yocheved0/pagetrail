using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PageTrail.Worker.Health;

public sealed class BrokerConnectionHealthCheck(BrokerConnectionState state) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(state.IsOpen
            ? HealthCheckResult.Healthy("RabbitMQ connection is open")
            : HealthCheckResult.Unhealthy("RabbitMQ connection is not open"));
}
