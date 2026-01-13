using System.Text.Json.Serialization;

namespace CatalogAPI.Domain.Events;

public class OrderPlacedEvent
{
    [JsonPropertyName("orderId")]
    public Guid OrderId { get; set; }
    
    [JsonPropertyName("userId")]
    public Guid UserId { get; set; }
    
    [JsonPropertyName("gameId")]
    public Guid GameId { get; set; }
    
    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    public OrderPlacedEvent()
    {
    }

    public OrderPlacedEvent(Guid orderId, Guid userId, Guid gameId, decimal price)
    {
        OrderId = orderId;
        UserId = userId;
        GameId = gameId;
        Price = price;
    }
}
