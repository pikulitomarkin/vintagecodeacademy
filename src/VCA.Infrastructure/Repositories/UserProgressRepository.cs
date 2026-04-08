using Microsoft.EntityFrameworkCore;
using VCA.Domain.Entities;
using VCA.Domain.Interfaces;
using VCA.Infrastructure.Data;

namespace VCA.Infrastructure.Repositories;

public class UserProgressRepository : BaseRepository<UserProgress>, IUserProgressRepository
{
    public UserProgressRepository(AppDbContext context) : base(context) { }

    public async Task<bool> HasCompletedAsync(Guid userId, Guid lessonId, CancellationToken cancellationToken = default)
        => await _dbSet.AnyAsync(p => p.UserId == userId && p.LessonId == lessonId, cancellationToken);

    public async Task<IReadOnlyList<UserProgress>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => await _dbSet.Where(p => p.UserId == userId).OrderByDescending(p => p.CompletedAt).ToListAsync(cancellationToken);
}
