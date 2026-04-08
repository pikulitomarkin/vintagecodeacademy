using Microsoft.EntityFrameworkCore;
using VCA.Domain.Entities;
using VCA.Domain.Interfaces;
using VCA.Infrastructure.Data;

namespace VCA.Infrastructure.Repositories;

public class TrailRepository : BaseRepository<Trail>, ITrailRepository
{
    public TrailRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Trail>> GetPublishedAsync(CancellationToken cancellationToken = default)
        => await _dbSet.Where(t => t.IsPublished).OrderBy(t => t.Order).ToListAsync(cancellationToken);

    public async Task<Trail?> GetWithModulesAsync(Guid trailId, CancellationToken cancellationToken = default)
        => await _dbSet.Include(t => t.Modules.OrderBy(m => m.Order))
                       .FirstOrDefaultAsync(t => t.Id == trailId, cancellationToken);
}
