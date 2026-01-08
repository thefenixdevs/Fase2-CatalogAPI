using Mediator;

namespace CatalogAPI.Application.Commands;

public sealed record PurchaseGameCommand(Guid GameId, Guid CorrelationId, Guid UserId) : ICommand<Guid>;
