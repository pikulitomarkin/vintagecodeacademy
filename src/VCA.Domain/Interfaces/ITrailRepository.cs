using VCA.Domain.Entities;

namespace VCA.Domain.Interfaces;

/// <summary>
/// Contrato de repositório específico para a entidade Trail.
/// </summary>
public interface ITrailRepository : IRepository<Trail>
{
    Task<IReadOnlyList<Trail>> GetPublishedAsync(CancellationToken cancellationToken = default);
    Task<Trail?> GetWithModulesAsync(Guid trailId, CancellationToken cancellationToken = default);
}
