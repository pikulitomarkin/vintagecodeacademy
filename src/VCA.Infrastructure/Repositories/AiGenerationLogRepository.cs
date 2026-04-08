using Microsoft.EntityFrameworkCore;
using VCA.Domain.Entities;
using VCA.Domain.Interfaces;
using VCA.Infrastructure.Data;

namespace VCA.Infrastructure.Repositories;

public class AiGenerationLogRepository : BaseRepository<AiGenerationLog>, IAiGenerationLogRepository
{
    public AiGenerationLogRepository(AppDbContext context) : base(context) { }

    public async Task<decimal> GetTotalCostByLessonAsync(Guid lessonId, CancellationToken cancellationToken = default)
        => await _dbSet.Where(l => l.LessonId == lessonId).SumAsync(l => l.CostUsd, cancellationToken);

    public async Task<decimal> GetTotalCostAsync(CancellationToken cancellationToken = default)
        => await _dbSet.SumAsync(l => l.CostUsd, cancellationToken);
}
