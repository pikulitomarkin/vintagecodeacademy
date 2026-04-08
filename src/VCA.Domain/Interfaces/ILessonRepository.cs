using VCA.Domain.Entities;
using VCA.Domain.Enums;

namespace VCA.Domain.Interfaces;

/// <summary>
/// Contrato de repositório específico para a entidade Lesson.
/// </summary>
public interface ILessonRepository : IRepository<Lesson>
{
    Task<IReadOnlyList<Lesson>> GetByModuleAsync(Guid moduleId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Lesson>> GetByStatusAsync(LessonStatus status, CancellationToken cancellationToken = default);
    Task<Lesson?> GetWithChunksAsync(Guid lessonId, CancellationToken cancellationToken = default);
}
