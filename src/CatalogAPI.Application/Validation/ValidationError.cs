namespace CatalogAPI.Application.Validation;

public record ValidationError(string Field, string Message);