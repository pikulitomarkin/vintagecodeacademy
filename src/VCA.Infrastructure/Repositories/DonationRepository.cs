using Microsoft.EntityFrameworkCore;
using VCA.Domain.Entities;
using VCA.Domain.Interfaces;
using VCA.Infrastructure.Data;

namespace VCA.Infrastructure.Repositories;

public class DonationRepository : BaseRepository<Donation>, IDonationRepository
{
    public DonationRepository(AppDbContext context) : base(context) { }

    public async Task<Donation?> GetByExternalReferenceAsync(string externalReference, CancellationToken cancellationToken = default)
        => await _dbSet.FirstOrDefaultAsync(d => d.ExternalReference == externalReference, cancellationToken);
}
