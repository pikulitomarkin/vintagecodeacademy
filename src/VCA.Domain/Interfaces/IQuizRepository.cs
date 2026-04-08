using VCA.Domain.Entities;

namespace VCA.Domain.Interfaces;

/// <summary>
/// Contrato de repositório específico para a entidade Quiz.
/// </summary>
public interface IQuizRepository : IRepository<Quiz>
{
    Task<IReadOnlyList<Quiz>> GetByLessonAsync(Guid lessonId, CancellationToken cancellationToken = default);
}
