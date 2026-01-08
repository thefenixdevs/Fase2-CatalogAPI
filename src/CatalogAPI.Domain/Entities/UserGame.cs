namespace CatalogAPI.Domain.Entities;

public class UserGame
{
    public Guid UserId { get; set; }
    public Guid GameId { get; set; }
    public DateTime PurchaseDate { get; set; }

    public Game Game { get; set; } = null!;
}
