namespace CatalogAPI.Application.DTOs;

public class UserGameDto
{
    public Guid UserId { get; set; }
    public Guid GameId { get; set; }
    public DateTime PurchaseDate { get; set; }
    public GameDto Game { get; set; } = null!;
}
