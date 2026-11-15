using System.Text.Json;
using Microsoft.Extensions.Options;
using PageTrail.Infrastructure.Messaging;
using RabbitMQ.Client;

namespace PageTrail.Api.Dispatching;

public sealed class RabbitMqPagePublisher(IOptions<RabbitMqOptions> rabbitMq, ILogger<RabbitMqPagePublisher> logger) : IPagePublisher
{
    private IConnection? _connection;
    private IChannel? _channel;

    public async Task PublishAsync(CrawlPageMessage message, CancellationToken cancellationToken)
    {
        var channel = await OpenChannelAsync(cancellationToken);
        var body = JsonSerializer.SerializeToUtf8Bytes(message);
        await channel.BasicPublishAsync(
            exchange: "", routingKey: CrawlQueues.Pages, mandatory: true, new BasicProperties { Persistent = true }, body, cancellationToken);
    }

    public async Task CloseAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
            _channel = null;
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }

    private async Task<IChannel> OpenChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
        {
            return _channel;
        }

        await CloseAsync();
        _connection = await rabbitMq.Value.CreateConnectionFactory().CreateConnectionAsync(cancellationToken);
        _channel = await _connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            cancellationToken);
        await CrawlQueues.DeclareAsync(_channel, cancellationToken);
        logger.LogInformation("Page dispatcher connected to RabbitMQ");
        return _channel;
    }
}
