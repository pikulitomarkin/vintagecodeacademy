using Microsoft.EntityFrameworkCore;
using VCA.Domain.Entities;
using VCA.Domain.Interfaces;
using VCA.Infrastructure.Data;

namespace VCA.Infrastructure.Repositories;

public class QuizAttemptRepository : BaseRepository<QuizAttempt>, IQuizAttemptRepository
{
    public QuizAttemptRepository(AppDbContext context) : base(context) { }

    public async Task<int> CountByUserAndLessonAsync(Guid userId, Guid lessonId, CancellationToken cancellationToken = default)
        => await _dbSet.CountAsync(a => a.UserId == userId && a.LessonId == lessonId, cancellationToken);

    public async Task<IReadOnlyList<QuizAttempt>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => await _dbSet.Where(a => a.UserId == userId).OrderByDescending(a => a.AttemptedAt).ToListAsync(cancellationToken);
}
