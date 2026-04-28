using VCA.Application.AI.Common;

namespace VCA.Application.Interfaces;

/// <summary>
/// Gerador de alto nível de conteúdo de aula e quiz.
/// Orquestra prompt-building, chamada à IA, validação e parsing.
/// </summary>
public interface IAiContentGenerator
{
    Task<LessonGenerationResult> GenerateLessonAsync(LessonGenerationRequest request, CancellationToken cancellationToken = default);
    Task<QuizGenerationResult> GenerateQuizAsync(QuizGenerationRequest request, CancellationToken cancellationToken = default);
}
