using CatalogAPI.Application.DTOs;
using Mediator;

namespace CatalogAPI.Application.Queries;

public sealed record GetGamesQuery(int PageNumber = 1, int PageSize = 20) : IQuery<PaginatedResultDto<GameDto>>;
