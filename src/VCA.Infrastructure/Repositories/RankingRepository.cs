using Microsoft.EntityFrameworkCore;
using VCA.Domain.Entities;
using VCA.Domain.Interfaces;
using VCA.Infrastructure.Data;

namespace VCA.Infrastructure.Repositories;

public class RankingRepository : BaseRepository<Ranking>, IRankingRepository
{
    public RankingRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Ranking>> GetWeeklyTopAsync(int week, int count, CancellationToken cancellationToken = default)
        => await _dbSet.Where(r => r.Week == week)
                       .OrderBy(r => r.Position)
                       .Take(count)
                       .Include(r => r.User)
                       .ToListAsync(cancellationToken);

    public async Task<Ranking?> GetByUserAndWeekAsync(Guid userId, int week, CancellationToken cancellationToken = default)
        => await _dbSet.FirstOrDefaultAsync(r => r.UserId == userId && r.Week == week, cancellationToken);
}
