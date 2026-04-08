namespace VCA.Application.Gamification.CompleteLesson;

/// <summary>
/// Comando para registrar a conclusão de uma aula por um usuário e conceder XP.
/// XP padrão por aula: +10. Por desafio: +15. Multiplicador de streak aplicado externamente.
/// </summary>
public record CompleteLessonCommand(Guid UserId, Guid LessonId);
