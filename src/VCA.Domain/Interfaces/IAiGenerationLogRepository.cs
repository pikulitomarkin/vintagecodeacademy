using VCA.Domain.Entities;

namespace VCA.Domain.Interfaces;

/// <summary>
/// Contrato de repositório específico para a entidade AiGenerationLog.
/// </summary>
public interface IAiGenerationLogRepository : IRepository<AiGenerationLog>
{
    Task<decimal> GetTotalCostByLessonAsync(Guid lessonId, CancellationToken cancellationToken = default);
    Task<decimal> GetTotalCostAsync(CancellationToken cancellationToken = default);
}
