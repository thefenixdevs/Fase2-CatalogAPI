using CatalogAPI.Domain.Entities;

namespace CatalogAPI.Domain.Interfaces;

public interface IUserGameRepository
{
    Task<UserGame?> GetByUserAndGameAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default);
    Task AddAsync(UserGame userGame, CancellationToken cancellationToken = default);
    Task RemoveAsync(UserGame userGame, CancellationToken cancellationToken = default);
}
