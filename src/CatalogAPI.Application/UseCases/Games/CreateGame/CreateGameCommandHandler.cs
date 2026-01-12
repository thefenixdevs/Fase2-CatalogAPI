using CatalogAPI.Application.DTOs;
using CatalogAPI.Domain.Entities;
using CatalogAPI.Domain.Exceptions;
using CatalogAPI.Domain.Interfaces;
using Mapster;
using Mediator;
using Microsoft.Extensions.Logging;

namespace CatalogAPI.Application.UseCases.Games.CreateGame;

public sealed class CreateGameCommandHandler : ICommandHandler<CreateGameCommand, Guid>
{
    private readonly IGameRepository _gameRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateGameCommandHandler> _logger;

    public CreateGameCommandHandler(
        IGameRepository gameRepository,
        IUnitOfWork unitOfWork,
        ILogger<CreateGameCommandHandler> logger)
    {
        _gameRepository = gameRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async ValueTask<Guid> Handle(CreateGameCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating new game: {GameName}", command.Game.Name);

        // Check if game with same name already exists
        var existsByName = await _gameRepository.ExistsByNameAsync(command.Game.Name, cancellationToken);
        if (existsByName)
        {
            throw new GameAlreadyExistsException(command.Game.Name);
        }

        // Map DTO to entity
        var game = command.Game.Adapt<Game>();
        game.Id = Guid.NewGuid();

        // Add game to repository
        await _gameRepository.AddAsync(game, cancellationToken);

        // Save changes
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Game created successfully. GameId: {GameId}, Name: {GameName}", 
            game.Id, game.Name);

        return game.Id;
    }
}
