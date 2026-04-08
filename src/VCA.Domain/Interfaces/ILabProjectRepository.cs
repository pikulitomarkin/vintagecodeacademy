using VCA.Domain.Entities;

namespace VCA.Domain.Interfaces;

/// <summary>
/// Contrato de repositório específico para a entidade LabProject.
/// </summary>
public interface ILabProjectRepository : IRepository<LabProject>
{
    Task<IReadOnlyList<LabProject>> GetOpenProjectsAsync(CancellationToken cancellationToken = default);
}
