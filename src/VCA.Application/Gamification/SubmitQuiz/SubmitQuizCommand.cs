namespace VCA.Application.Gamification.SubmitQuiz;

/// <summary>
/// Comando para submeter as respostas de um quiz.
/// XP por quiz: +30. Máximo 2 tentativas por aula (seed = user_id + lesson_id).
/// </summary>
public record SubmitQuizCommand(
    Guid UserId,
    Guid LessonId,
    List<int> SelectedAnswers
);
