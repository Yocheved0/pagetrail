using Microsoft.Extensions.Options;
using PageTrail.Infrastructure.Messaging;
using PageTrail.Worker.Health;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PageTrail.Worker.Processing;

public sealed class CrawlPageConsumer(
    IServiceScopeFactory scopes,
    IOptions<RabbitMqOptions> rabbitMq,
    BrokerConnectionState connectionState,
    ILogger<CrawlPageConsumer> logger) : BackgroundService
{
    private const ushort Prefetch = 10;
    private const int FirstAttempt = 1;
    private static readonly TimeSpan RequeueDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var connection = await rabbitMq.Value.CreateConnectionFactory().CreateConnectionAsync(stoppingToken);
        connectionState.Track(connection);
        await using var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            stoppingToken);
        await CrawlQueues.DeclareAsync(channel, stoppingToken);
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: Prefetch, global: false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, delivery) => HandleAsync(channel, delivery, stoppingToken);
        await channel.BasicConsumeAsync(CrawlQueues.Pages, autoAck: false, consumer, stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private static int AttemptOf(IReadOnlyBasicProperties properties)
    {
        object? header = null;
        properties.Headers?.TryGetValue(CrawlQueues.AttemptHeader, out header);
        return header switch
        {
            int attempt => attempt,
            long attempt => (int)attempt,
            _ => FirstAttempt,
        };
    }

    private static Task RouteAsync(IChannel channel, BasicDeliverEventArgs delivery, DeliveryDecision decision, CancellationToken cancellationToken) =>
        decision.Action switch
        {
            DeliveryAction.Retry => PublishAsync(channel, CrawlQueues.Retry, delivery.Body, decision.NextAttempt, cancellationToken),
            DeliveryAction.DeadLetter => PublishAsync(channel, CrawlQueues.DeadLetter, delivery.Body, AttemptOf(delivery.BasicProperties), cancellationToken),
            _ => Task.CompletedTask,
        };

    private static async Task PublishAsync(IChannel channel, string queue, ReadOnlyMemory<byte> body, int attempt, CancellationToken cancellationToken)
    {
        var properties = new BasicProperties
        {
            Persistent = true,
            Headers = new Dictionary<string, object?> { [CrawlQueues.AttemptHeader] = attempt },
        };
        await channel.BasicPublishAsync(exchange: "", routingKey: queue, mandatory: true, properties, body, cancellationToken);
    }

    private Task HandleAsync(IChannel channel, BasicDeliverEventArgs delivery, CancellationToken cancellationToken) =>
        DeliverySafetyNet.RunAsync(
            () => DeliverAsync(channel, delivery, cancellationToken),
            () => channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: true, cancellationToken).AsTask(),
            RequeueDelay,
            logger,
            cancellationToken);

    private async Task DeliverAsync(IChannel channel, BasicDeliverEventArgs delivery, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<CrawlDeliveryHandler>();
        var decision = await handler.HandleAsync(delivery.Body, AttemptOf(delivery.BasicProperties), cancellationToken);
        await RouteAsync(channel, delivery, decision, cancellationToken);
        await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);
    }
}
