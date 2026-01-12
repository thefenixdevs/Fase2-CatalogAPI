namespace CatalogAPI.Application.DTOs;

public class CreateGameDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Genre { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string Developer { get; set; } = string.Empty;
    public DateTime ReleaseDate { get; set; }
}
