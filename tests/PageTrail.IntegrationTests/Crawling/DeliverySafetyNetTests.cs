using Microsoft.Extensions.Logging.Abstractions;
using PageTrail.Worker.Processing;

namespace PageTrail.IntegrationTests.Crawling;

public sealed class DeliverySafetyNetTests
{
    [Fact]
    public async Task ADeliveryThatThrowsIsRequeued()
    {
        var requeued = 0;

        await DeliverySafetyNet.RunAsync(
            () => throw new InvalidOperationException("database unavailable"),
            () => { requeued++; return Task.CompletedTask; },
            TimeSpan.Zero,
            NullLogger.Instance,
            CancellationToken.None);

        Assert.Equal(1, requeued);
    }

    [Fact]
    public async Task ASuccessfulDeliveryIsNotRequeued()
    {
        var requeued = 0;

        await DeliverySafetyNet.RunAsync(
            () => Task.CompletedTask,
            () => { requeued++; return Task.CompletedTask; },
            TimeSpan.Zero,
            NullLogger.Instance,
            CancellationToken.None);

        Assert.Equal(0, requeued);
    }

    [Fact]
    public async Task ShutdownCancellationIsNotSwallowed()
    {
        using var shutdown = new CancellationTokenSource();
        await shutdown.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => DeliverySafetyNet.RunAsync(
            () => throw new OperationCanceledException(shutdown.Token),
            () => Task.CompletedTask,
            TimeSpan.Zero,
            NullLogger.Instance,
            shutdown.Token));
    }
}
