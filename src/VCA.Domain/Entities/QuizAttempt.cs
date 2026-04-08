namespace VCA.Domain.Entities;

/// <summary>
/// Registro de uma tentativa de quiz feita por um usuário.
/// Máximo de 2 tentativas por aula.
/// </summary>
public class QuizAttempt
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid LessonId { get; private set; }
    public int Score { get; private set; }

    /// <summary>JSON com as respostas selecionadas pelo usuário (índices).</summary>
    public string AnswersJson { get; private set; } = "[]";

    public DateTime AttemptedAt { get; private set; }

    public User? User { get; private set; }
    public Lesson? Lesson { get; private set; }

    private QuizAttempt() { }

    public static QuizAttempt Create(Guid userId, Guid lessonId, int score, string answersJson)
    {
        return new QuizAttempt
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            LessonId = lessonId,
            Score = score,
            AnswersJson = answersJson,
            AttemptedAt = DateTime.UtcNow
        };
    }
}
