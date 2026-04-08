using VCA.Domain.Entities;

namespace VCA.Domain.Interfaces;

/// <summary>
/// Contrato de repositório específico para a entidade UserProgress.
/// </summary>
public interface IUserProgressRepository : IRepository<UserProgress>
{
    Task<bool> HasCompletedAsync(Guid userId, Guid lessonId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserProgress>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
