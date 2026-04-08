using VCA.Domain.Entities;

namespace VCA.Domain.Interfaces;

/// <summary>
/// Contrato de repositório específico para a entidade QuizAttempt.
/// </summary>
public interface IQuizAttemptRepository : IRepository<QuizAttempt>
{
    Task<int> CountByUserAndLessonAsync(Guid userId, Guid lessonId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QuizAttempt>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
