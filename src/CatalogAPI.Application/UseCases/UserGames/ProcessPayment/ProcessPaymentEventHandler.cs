using CatalogAPI.Domain.Entities;
using CatalogAPI.Domain.Events;
using CatalogAPI.Domain.Exceptions;
using CatalogAPI.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace CatalogAPI.Application.UseCases.UserGames.ProcessPayment;

public sealed class ProcessPaymentEventHandler
{
    private readonly IGameRepository _gameRepository;
    private readonly IUserGameRepository _userGameRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ProcessPaymentEventHandler> _logger;

    public ProcessPaymentEventHandler(
        IGameRepository gameRepository,
        IUserGameRepository userGameRepository,
        IUnitOfWork unitOfWork,
        ILogger<ProcessPaymentEventHandler> logger)
    {
        _gameRepository = gameRepository;
        _userGameRepository = userGameRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async ValueTask Handle(PaymentProcessedEvent message, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing PaymentProcessedEvent. CorrelationId: {CorrelationId}, Status: {Status}, UserId: {UserId}, GameId: {GameId}",
            message.CorrelationId, message.Status, message.UserId, message.GameId);

        // Only add game to library if payment was approved
        if (message.Status != "Approved")
        {
            _logger.LogWarning("Payment was not approved (Status: {Status}). Game will not be added to library. CorrelationId: {CorrelationId}",
                message.Status, message.CorrelationId);
            return;
        }

        // Validate game exists
        var game = await _gameRepository.GetByIdAsync(message.GameId, cancellationToken);
        if (game == null)
        {
            _logger.LogError("Game not found when processing payment. GameId: {GameId}, CorrelationId: {CorrelationId}",
                message.GameId, message.CorrelationId);
            return;
        }

        // Check if user already owns the game (idempotency check)
        var existingPurchase = await _userGameRepository.GetByUserAndGameAsync(message.UserId, message.GameId, cancellationToken);
        if (existingPurchase != null)
        {
            _logger.LogInformation("User already owns this game. Skipping. UserId: {UserId}, GameId: {GameId}, CorrelationId: {CorrelationId}",
                message.UserId, message.GameId, message.CorrelationId);
            return;
        }

        try
        {
            // Add game to user's library
            var userGame = new UserGame
            {
                UserId = message.UserId,
                GameId = message.GameId,
                PurchaseDate = DateTime.UtcNow
            };

            await _userGameRepository.AddAsync(userGame, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Game successfully added to user library. UserId: {UserId}, GameId: {GameId}, CorrelationId: {CorrelationId}",
                message.UserId, message.GameId, message.CorrelationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding game to library after payment approval. UserId: {UserId}, GameId: {GameId}, CorrelationId: {CorrelationId}",
                message.UserId, message.GameId, message.CorrelationId);
            throw;
        }
    }
}
