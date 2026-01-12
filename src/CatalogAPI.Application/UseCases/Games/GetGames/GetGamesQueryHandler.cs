using CatalogAPI.Application.DTOs;
using CatalogAPI.Domain.Interfaces;
using Mapster;
using Mediator;

namespace CatalogAPI.Application.UseCases.Games.GetGames;

public sealed class GetGamesQueryHandler : IQueryHandler<GetGamesQuery, PaginatedResultDto<GameDto>>
{
    private readonly IGameRepository _gameRepository;

    public GetGamesQueryHandler(IGameRepository gameRepository)
    {
        _gameRepository = gameRepository;
    }

    public async ValueTask<PaginatedResultDto<GameDto>> Handle(GetGamesQuery query, CancellationToken cancellationToken)
    {
        var games = await _gameRepository.GetAllAsync(query.PageNumber, query.PageSize, cancellationToken);
        var totalCount = await _gameRepository.GetTotalCountAsync(cancellationToken);

        var gameDtos = games.Adapt<List<GameDto>>();

        return new PaginatedResultDto<GameDto>
        {
            Items = gameDtos,
            TotalCount = totalCount,
            PageNumber = query.PageNumber,
            PageSize = query.PageSize
        };
    }
}
