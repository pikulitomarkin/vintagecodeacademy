using Microsoft.EntityFrameworkCore;
using VCA.Domain.Entities;
using VCA.Domain.Interfaces;
using VCA.Infrastructure.Data;

namespace VCA.Infrastructure.Repositories;

public class ModuleRepository : BaseRepository<Module>, IModuleRepository
{
    public ModuleRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Module>> GetByTrailAsync(Guid trailId, CancellationToken cancellationToken = default)
        => await _dbSet.Where(m => m.TrailId == trailId).OrderBy(m => m.Order).ToListAsync(cancellationToken);
}
