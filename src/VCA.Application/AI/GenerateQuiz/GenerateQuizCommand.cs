namespace VCA.Application.AI.GenerateQuiz;

/// <summary>
/// Comando para gerar um pool de questões de quiz para uma aula via DeepSeek.
/// </summary>
public record GenerateQuizCommand(Guid LessonId, int QuestionCount = 10);
