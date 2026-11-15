using System.Text.Json;
using Microsoft.Extensions.Options;
using PageTrail.Infrastructure.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PageTrail.Worker.Processing;

public sealed class CrawlPageConsumer(
    IServiceScopeFactory scopes,
    IOptions<RabbitMqOptions> rabbitMq,
    ILogger<CrawlPageConsumer> logger) : BackgroundService
{
    private const ushort Prefetch = 10;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var connection = await rabbitMq.Value.CreateConnectionFactory().CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        await CrawlQueues.DeclareAsync(channel, stoppingToken);
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: Prefetch, global: false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, delivery) => HandleAsync(channel, delivery, stoppingToken);
        await channel.BasicConsumeAsync(CrawlQueues.Pages, autoAck: false, consumer, stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleAsync(IChannel channel, BasicDeliverEventArgs delivery, CancellationToken cancellationToken)
    {
        var message = JsonSerializer.Deserialize<CrawlPageMessage>(delivery.Body.Span)!;
        using var logScope = logger.BeginScope(new Dictionary<string, object> { ["JobId"] = message.JobId, ["PageId"] = message.PageId });
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<CrawlPageProcessor>().ProcessAsync(message, cancellationToken);
            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Page {PageId} failed and was dropped", message.PageId);
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, cancellationToken);
        }
    }
}
