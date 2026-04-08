using VCA.Domain.Entities;

namespace VCA.Domain.Interfaces;

/// <summary>
/// Contrato de repositório específico para a entidade Badge.
/// </summary>
public interface IBadgeRepository : IRepository<Badge>
{
    Task<Badge?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Badge>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
