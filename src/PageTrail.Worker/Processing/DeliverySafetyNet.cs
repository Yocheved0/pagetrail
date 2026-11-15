namespace PageTrail.Worker.Processing;

public static class DeliverySafetyNet
{
    public static async Task RunAsync(
        Func<Task> deliver,
        Func<Task> requeue,
        TimeSpan requeueDelay,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            await deliver();
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // WHY: if routing itself fails (database or broker down) the message must not sit unacked on an open channel
            logger.LogError(ex, "Delivery could not be routed; requeueing in {DelaySeconds} s", requeueDelay.TotalSeconds);
            await Task.Delay(requeueDelay, cancellationToken);
            await requeue();
        }
    }
}
