using CatalogAPI.Application.DTOs;
using CatalogAPI.Domain.Exceptions;
using CatalogAPI.Domain.Interfaces;
using Mediator;
using Microsoft.Extensions.Logging;

namespace CatalogAPI.Application.UseCases.Games.UpdateGame;

public sealed class UpdateGameCommandHandler : ICommandHandler<UpdateGameCommand, bool>
{
    private readonly IGameRepository _gameRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateGameCommandHandler> _logger;

    public UpdateGameCommandHandler(
        IGameRepository gameRepository,
        IUnitOfWork unitOfWork,
        ILogger<UpdateGameCommandHandler> logger)
    {
        _gameRepository = gameRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async ValueTask<bool> Handle(UpdateGameCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating game: {GameId}", command.GameId);

        // Get existing game with tracking for update
        var game = await _gameRepository.GetByIdForUpdateAsync(command.GameId, cancellationToken);
        if (game == null)
        {
            throw new GameNotFoundException(command.GameId);
        }

        // Update properties if provided
        if (command.Game.Name != null)
            game.Name = command.Game.Name;

        if (command.Game.Description != null)
            game.Description = command.Game.Description;

        if (command.Game.Price.HasValue)
            game.Price = command.Game.Price.Value;

        if (command.Game.Genre != null)
            game.Genre = command.Game.Genre;

        if (command.Game.ImageUrl != null)
            game.ImageUrl = command.Game.ImageUrl;

        if (command.Game.Developer != null)
            game.Developer = command.Game.Developer;

        if (command.Game.ReleaseDate.HasValue)
            game.ReleaseDate = command.Game.ReleaseDate.Value;

        // Update game in repository
        await _gameRepository.UpdateAsync(game, cancellationToken);

        // Save changes
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Game updated successfully. GameId: {GameId}", command.GameId);

        return true;
    }
}
