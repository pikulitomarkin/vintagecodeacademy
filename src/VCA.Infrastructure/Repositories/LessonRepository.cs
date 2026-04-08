using Microsoft.EntityFrameworkCore;
using VCA.Domain.Entities;
using VCA.Domain.Enums;
using VCA.Domain.Interfaces;
using VCA.Infrastructure.Data;

namespace VCA.Infrastructure.Repositories;

public class LessonRepository : BaseRepository<Lesson>, ILessonRepository
{
    public LessonRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Lesson>> GetByModuleAsync(Guid moduleId, CancellationToken cancellationToken = default)
        => await _dbSet.Where(l => l.ModuleId == moduleId).OrderBy(l => l.Order).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Lesson>> GetByStatusAsync(LessonStatus status, CancellationToken cancellationToken = default)
        => await _dbSet.Where(l => l.Status == status).ToListAsync(cancellationToken);

    public async Task<Lesson?> GetWithChunksAsync(Guid lessonId, CancellationToken cancellationToken = default)
        => await _dbSet.Include(l => l.Chunks.OrderBy(c => c.ChunkIndex))
                       .FirstOrDefaultAsync(l => l.Id == lessonId, cancellationToken);
}
