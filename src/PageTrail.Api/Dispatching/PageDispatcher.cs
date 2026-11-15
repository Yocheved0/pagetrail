using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PageTrail.Infrastructure.Messaging;
using PageTrail.Infrastructure.Persistence;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace PageTrail.Api.Dispatching;

public sealed class PageDispatcher(
    IServiceScopeFactory scopes,
    IOptions<RabbitMqOptions> rabbitMq,
    ILogger<PageDispatcher> logger) : BackgroundService
{
    private const int BatchSize = 100;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    private IConnection? _connection;
    private IChannel? _channel;

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await CloseAsync();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await PublishBatchAsync(await OpenChannelAsync(stoppingToken), stoppingToken);
            }
            catch (Exception ex) when (ex is RabbitMQClientException or IOException)
            {
                logger.LogWarning(ex, "Page dispatch failed; retrying on the next tick");
                await CloseAsync();
            }
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

    private async Task CloseAsync()
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

    private async Task PublishBatchAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PageTrailDbContext>();
        var pages = await db.Pages.Where(p => !p.IsPublished).OrderBy(p => p.Id).Take(BatchSize).ToListAsync(cancellationToken);

        foreach (var page in pages)
        {
            var body = JsonSerializer.SerializeToUtf8Bytes(new CrawlPageMessage(page.JobId, page.Id));
            await channel.BasicPublishAsync(
                exchange: "", routingKey: CrawlQueues.Pages, mandatory: true, new BasicProperties { Persistent = true }, body, cancellationToken);
            page.MarkPublished();
            logger.LogInformation("Page {PageId} of job {JobId} published", page.Id, page.JobId);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
