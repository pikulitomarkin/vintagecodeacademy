using Microsoft.EntityFrameworkCore;
using VCA.Domain.Entities;
using VCA.Domain.Interfaces;
using VCA.Infrastructure.Data;

namespace VCA.Infrastructure.Repositories;

public class QuizRepository : BaseRepository<Quiz>, IQuizRepository
{
    public QuizRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Quiz>> GetByLessonAsync(Guid lessonId, CancellationToken cancellationToken = default)
        => await _dbSet.Where(q => q.LessonId == lessonId).ToListAsync(cancellationToken);
}
