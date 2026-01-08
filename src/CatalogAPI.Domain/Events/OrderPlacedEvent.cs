namespace CatalogAPI.Domain.Events;

public class OrderPlacedEvent
{
    public Guid CorrelationId { get; set; }
    public Guid UserId { get; set; }
    public Guid GameId { get; set; }
    public decimal Price { get; set; }
    public DateTime OccurredAt { get; set; }

    public OrderPlacedEvent(Guid correlationId, Guid userId, Guid gameId, decimal price)
    {
        CorrelationId = correlationId;
        UserId = userId;
        GameId = gameId;
        Price = price;
        OccurredAt = DateTime.UtcNow;
    }
}
