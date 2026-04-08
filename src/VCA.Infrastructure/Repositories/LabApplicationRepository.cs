using Microsoft.EntityFrameworkCore;
using VCA.Domain.Entities;
using VCA.Domain.Interfaces;
using VCA.Infrastructure.Data;

namespace VCA.Infrastructure.Repositories;

public class LabApplicationRepository : BaseRepository<LabApplication>, ILabApplicationRepository
{
    public LabApplicationRepository(AppDbContext context) : base(context) { }

    public async Task<bool> HasAppliedAsync(Guid userId, Guid projectId, CancellationToken cancellationToken = default)
        => await _dbSet.AnyAsync(a => a.UserId == userId && a.ProjectId == projectId, cancellationToken);

    public async Task<IReadOnlyList<LabApplication>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => await _dbSet.Where(a => a.UserId == userId).Include(a => a.Project).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LabApplication>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
        => await _dbSet.Where(a => a.ProjectId == projectId).Include(a => a.User).ToListAsync(cancellationToken);
}
