using VCA.Domain.Entities;

namespace VCA.Domain.Interfaces;

/// <summary>
/// Contrato de repositório específico para a entidade User.
/// </summary>
public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetTopByXpAsync(int count, CancellationToken cancellationToken = default);
}
