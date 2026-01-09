using CatalogAPI.Domain.Events;

namespace CatalogAPI.Application.Events;

public interface IEventHandler<in TEvent>
{
    Task HandleAsync(UserCreatedEvent domainEvent, CancellationToken cancellationToken = default);
}