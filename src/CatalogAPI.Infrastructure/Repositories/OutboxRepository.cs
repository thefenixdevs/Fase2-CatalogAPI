using CatalogAPI.Domain.Entities;
using CatalogAPI.Domain.Interfaces;
using CatalogAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CatalogAPI.Infrastructure.Repositories;

public class OutboxRepository : IOutboxRepository
{
    private readonly CatalogDbContext _context;

    public OutboxRepository(CatalogDbContext context)
    {
        _context = context;
    }

    public async Task<List<OutboxMessage>> GetUnprocessedBatchAsync(int batchSize = 100, CancellationToken cancellationToken = default)
    {
        return await _context.OutboxMessages
            .Where(om => om.ProcessedAt == null)
            .OrderBy(om => om.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(OutboxMessage outboxMessage, CancellationToken cancellationToken = default)
    {
        await _context.OutboxMessages.AddAsync(outboxMessage, cancellationToken);
    }

    public async Task MarkAsProcessedAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        var message = await _context.OutboxMessages.FindAsync(new object[] { messageId }, cancellationToken);
        if (message != null)
        {
            message.ProcessedAt = DateTime.UtcNow;
        }
    }
}
