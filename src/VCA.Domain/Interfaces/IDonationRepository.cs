using VCA.Domain.Entities;

namespace VCA.Domain.Interfaces;

/// <summary>
/// Contrato de repositório específico para a entidade Donation.
/// </summary>
public interface IDonationRepository : IRepository<Donation>
{
    Task<Donation?> GetByExternalReferenceAsync(string externalReference, CancellationToken cancellationToken = default);
}
