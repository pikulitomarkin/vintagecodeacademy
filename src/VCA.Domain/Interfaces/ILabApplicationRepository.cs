using VCA.Domain.Entities;

namespace VCA.Domain.Interfaces;

/// <summary>
/// Contrato de repositório específico para a entidade LabApplication.
/// </summary>
public interface ILabApplicationRepository : IRepository<LabApplication>
{
    Task<bool> HasAppliedAsync(Guid userId, Guid projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LabApplication>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LabApplication>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
}
