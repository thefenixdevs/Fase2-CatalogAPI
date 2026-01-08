using CatalogAPI.Domain.Entities;

namespace CatalogAPI.Domain.Interfaces;

public interface IOutboxRepository
{
    Task<List<OutboxMessage>> GetUnprocessedBatchAsync(int batchSize = 100, CancellationToken cancellationToken = default);
    Task AddAsync(OutboxMessage outboxMessage, CancellationToken cancellationToken = default);
    Task MarkAsProcessedAsync(Guid messageId, CancellationToken cancellationToken = default);
}
