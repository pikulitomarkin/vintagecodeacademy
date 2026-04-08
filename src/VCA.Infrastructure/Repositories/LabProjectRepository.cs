using Microsoft.EntityFrameworkCore;
using VCA.Domain.Entities;
using VCA.Domain.Interfaces;
using VCA.Infrastructure.Data;

namespace VCA.Infrastructure.Repositories;

public class LabProjectRepository : BaseRepository<LabProject>, ILabProjectRepository
{
    public LabProjectRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<LabProject>> GetOpenProjectsAsync(CancellationToken cancellationToken = default)
        => await _dbSet.Where(p => p.Status == "open" && p.SlotsAvailable > 0).ToListAsync(cancellationToken);
}
