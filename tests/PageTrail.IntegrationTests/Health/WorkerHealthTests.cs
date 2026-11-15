using System.Net;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using PageTrail.Worker.Health;
using RabbitMQ.Client;

namespace PageTrail.IntegrationTests.Health;

[Collection(ApiCollection.Name)]
public sealed class WorkerHealthTests(PageTrailApiFactory api)
{
    [Fact]
    public async Task WorkerHealthReturns200WhenTheDatabaseIsReachableAndTheBrokerConnectionIsOpen()
    {
        EnsureTestDatabaseIsMigrated();
        await using var worker = new PageTrailWorkerFactory();
        worker.Services.GetRequiredService<BrokerConnectionState>().Track(OpenBrokerConnection.Create());
        using var client = worker.CreateClient();

        var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task WorkerHealthReturns503WhenNoBrokerConnectionIsOpen()
    {
        EnsureTestDatabaseIsMigrated();
        await using var worker = new PageTrailWorkerFactory();
        using var client = worker.CreateClient();

        var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    private void EnsureTestDatabaseIsMigrated() => _ = api.Services;

    public class OpenBrokerConnection : DispatchProxy
    {
        public static IConnection Create() => Create<IConnection, OpenBrokerConnection>();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.Name == $"get_{nameof(IConnection.IsOpen)}"
                ? true
                : throw new NotSupportedException(targetMethod?.Name);
    }
}
