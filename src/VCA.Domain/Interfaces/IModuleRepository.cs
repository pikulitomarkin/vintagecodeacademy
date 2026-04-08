using VCA.Domain.Entities;

namespace VCA.Domain.Interfaces;

/// <summary>
/// Contrato de repositório específico para a entidade Module.
/// </summary>
public interface IModuleRepository : IRepository<Module>
{
    Task<IReadOnlyList<Module>> GetByTrailAsync(Guid trailId, CancellationToken cancellationToken = default);
}
