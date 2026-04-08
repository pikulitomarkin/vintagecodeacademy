using VCA.Domain.Enums;

namespace VCA.Application.Gamification.CompleteLesson;

/// <summary>
/// Resultado da conclusão de uma aula.
/// </summary>
public record CompleteLessonResult(int XpEarned, int TotalXp, UserLevel NewLevel);
