using Microsoft.EntityFrameworkCore;
using VCA.Domain.Entities;
using VCA.Domain.Interfaces;
using VCA.Infrastructure.Data;

namespace VCA.Infrastructure.Repositories;

public class UserRepository : BaseRepository<User>, IUserRepository
{
    public UserRepository(AppDbContext context) : base(context) { }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => await _dbSet.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    public async Task<IReadOnlyList<User>> GetTopByXpAsync(int count, CancellationToken cancellationToken = default)
        => await _dbSet.OrderByDescending(u => u.Xp).Take(count).ToListAsync(cancellationToken);
}
