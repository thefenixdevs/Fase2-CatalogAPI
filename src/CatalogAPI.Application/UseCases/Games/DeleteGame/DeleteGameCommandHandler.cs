using CatalogAPI.Domain.Exceptions;
using CatalogAPI.Domain.Interfaces;
using Mediator;
using Microsoft.Extensions.Logging;

namespace CatalogAPI.Application.UseCases.Games.DeleteGame;

public sealed class DeleteGameCommandHandler : ICommandHandler<DeleteGameCommand, bool>
{
    private readonly IGameRepository _gameRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeleteGameCommandHandler> _logger;

    public DeleteGameCommandHandler(
        IGameRepository gameRepository,
        IUnitOfWork unitOfWork,
        ILogger<DeleteGameCommandHandler> logger)
    {
        _gameRepository = gameRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async ValueTask<bool> Handle(DeleteGameCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting game: {GameId}", command.GameId);

        // Check if game exists
        var exists = await _gameRepository.ExistsAsync(command.GameId, cancellationToken);
        if (!exists)
        {
            throw new GameNotFoundException(command.GameId);
        }

        // Delete game (cascade delete will handle UserGames)
        await _gameRepository.DeleteAsync(command.GameId, cancellationToken);

        // Save changes
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Game deleted successfully. GameId: {GameId}", command.GameId);

        return true;
    }
}
