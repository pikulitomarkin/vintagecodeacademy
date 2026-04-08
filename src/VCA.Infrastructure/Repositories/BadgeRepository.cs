using Microsoft.EntityFrameworkCore;
using VCA.Domain.Entities;
using VCA.Domain.Interfaces;
using VCA.Infrastructure.Data;

namespace VCA.Infrastructure.Repositories;

public class BadgeRepository : BaseRepository<Badge>, IBadgeRepository
{
    public BadgeRepository(AppDbContext context) : base(context) { }

    public async Task<Badge?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        => await _dbSet.FirstOrDefaultAsync(b => b.Code == code, cancellationToken);

    public async Task<IReadOnlyList<Badge>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => await _context.UserBadges
            .Where(ub => ub.UserId == userId)
            .Include(ub => ub.Badge)
            .Select(ub => ub.Badge!)
            .ToListAsync(cancellationToken);
}
