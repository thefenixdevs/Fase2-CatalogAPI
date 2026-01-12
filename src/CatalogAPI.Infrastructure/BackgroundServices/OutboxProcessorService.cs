using CatalogAPI.Domain.Entities;
using CatalogAPI.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Wolverine;

namespace CatalogAPI.Infrastructure.BackgroundServices;

public class OutboxProcessorService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxProcessorService> _logger;
    private const int BatchSize = 100;
    private const int ProcessingIntervalSeconds = 5;

    public OutboxProcessorService(
        IServiceProvider serviceProvider,
        ILogger<OutboxProcessorService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox Processor Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox messages");
            }

            await Task.Delay(TimeSpan.FromSeconds(ProcessingIntervalSeconds), stoppingToken);
        }

        _logger.LogInformation("Outbox Processor Service stopped");
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();
        var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var unprocessedMessages = await outboxRepository.GetUnprocessedMessagesAsync(BatchSize, cancellationToken);

        if (unprocessedMessages.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Processing {Count} outbox messages", unprocessedMessages.Count);

        foreach (var message in unprocessedMessages)
        {
            try
            {
                // Deserialize the message payload
                var eventType = Type.GetType(message.EventType);
                if (eventType == null)
                {
                    _logger.LogWarning("Event type {EventType} not found, marking as processed", message.EventType);
                    await outboxRepository.MarkAsProcessedAsync(message.Id, cancellationToken);
                    continue;
                }

                var eventObject = JsonSerializer.Deserialize(message.Payload, eventType);
                if (eventObject == null)
                {
                    _logger.LogWarning("Failed to deserialize message {MessageId}", message.Id);
                    await outboxRepository.MarkAsProcessedAsync(message.Id, cancellationToken);
                    continue;
                }

                // Publish to Wolverine/RabbitMQ
                await messageBus.PublishAsync(eventObject);

                // Mark as processed
                await outboxRepository.MarkAsProcessedAsync(message.Id, cancellationToken);

                _logger.LogDebug("Processed outbox message {MessageId} of type {EventType}", 
                    message.Id, message.EventType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox message {MessageId}", message.Id);
                // Don't mark as processed on error - will retry on next iteration
            }
        }

        // Save all changes
        await outboxRepository.SaveChangesAsync(cancellationToken);
    }
}
