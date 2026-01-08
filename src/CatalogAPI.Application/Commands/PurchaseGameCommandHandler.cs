using System.Text.Json;
using CatalogAPI.Domain.Entities;
using CatalogAPI.Domain.Events;
using CatalogAPI.Domain.Exceptions;
using CatalogAPI.Domain.Interfaces;
using MassTransit;
using Mediator;
using Microsoft.Extensions.Logging;

namespace CatalogAPI.Application.Commands;

public sealed class PurchaseGameCommandHandler : ICommandHandler<PurchaseGameCommand, Guid>
{
    private readonly IGameRepository _gameRepository;
    private readonly IUserGameRepository _userGameRepository;
    private readonly IOutboxRepository _outboxRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<PurchaseGameCommandHandler> _logger;

    public PurchaseGameCommandHandler(
        IGameRepository gameRepository,
        IUserGameRepository userGameRepository,
        IOutboxRepository outboxRepository,
        IUnitOfWork unitOfWork,
        IPublishEndpoint publishEndpoint,
        ILogger<PurchaseGameCommandHandler> logger)
    {
        _gameRepository = gameRepository;
        _userGameRepository = userGameRepository;
        _outboxRepository = outboxRepository;
        _unitOfWork = unitOfWork;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async ValueTask<Guid> Handle(PurchaseGameCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing purchase for GameId: {GameId}, UserId: {UserId}, CorrelationId: {CorrelationId}", 
            command.GameId, command.UserId, command.CorrelationId);

        // Validate game exists
        var game = await _gameRepository.GetByIdAsync(command.GameId, cancellationToken);
        if (game == null)
        {
            throw new GameNotFoundException(command.GameId);
        }

        // Check if user already owns the game
        var existingPurchase = await _userGameRepository.GetByUserAndGameAsync(command.UserId, command.GameId, cancellationToken);
        if (existingPurchase != null)
        {
            throw new GameAlreadyPurchasedException(command.UserId, command.GameId);
        }

        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            // Create UserGame record
            var userGame = new UserGame
            {
                UserId = command.UserId,
                GameId = command.GameId,
                PurchaseDate = DateTime.UtcNow
            };

            await _userGameRepository.AddAsync(userGame, cancellationToken);

            // Create OrderPlacedEvent
            var orderPlacedEvent = new OrderPlacedEvent(command.CorrelationId, command.UserId, command.GameId, game.Price);

            // Save event to outbox for guaranteed delivery
            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                EventType = nameof(OrderPlacedEvent),
                Payload = JsonSerializer.Serialize(orderPlacedEvent),
                CreatedAt = DateTime.UtcNow,
                CorrelationId = command.CorrelationId.ToString()
            };

            await _outboxRepository.AddAsync(outboxMessage, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Attempt to publish event immediately
            try
            {
                await _publishEndpoint.Publish(orderPlacedEvent, cancellationToken);
                _logger.LogInformation("OrderPlacedEvent published successfully for CorrelationId: {CorrelationId}", command.CorrelationId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish OrderPlacedEvent immediately. Will be retried by OutboxProcessor. CorrelationId: {CorrelationId}", 
                    command.CorrelationId);
                
                // Rollback transaction and remove database records
                await _unitOfWork.RollbackAndThrowAsync(
                    new PublishEventFailedException("Failed to publish OrderPlacedEvent. Transaction rolled back.", ex), 
                    cancellationToken);
            }

            await _unitOfWork.CommitAsync(cancellationToken);
            
            _logger.LogInformation("Purchase completed successfully for GameId: {GameId}, UserId: {UserId}", 
                command.GameId, command.UserId);

            return userGame.GameId;
        }
        catch (Exception ex) when (ex is not GameAlreadyPurchasedException && ex is not GameNotFoundException)
        {
            _logger.LogError(ex, "Error processing purchase for GameId: {GameId}, UserId: {UserId}", 
                command.GameId, command.UserId);
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
