using VCA.Domain.Entities;

namespace VCA.Domain.Interfaces;

/// <summary>
/// Contrato de repositório específico para a entidade Ranking.
/// </summary>
public interface IRankingRepository : IRepository<Ranking>
{
    Task<IReadOnlyList<Ranking>> GetWeeklyTopAsync(int week, int count, CancellationToken cancellationToken = default);
    Task<Ranking?> GetByUserAndWeekAsync(Guid userId, int week, CancellationToken cancellationToken = default);
}
