namespace VCA.Domain.Entities;

/// <summary>
/// Questão de quiz vinculada a uma aula.
/// Pool de 10 questões por módulo; o aluno recebe 5 (seed = user_id + lesson_id), máximo 2 tentativas.
/// </summary>
public class Quiz
{
    public Guid Id { get; private set; }
    public Guid LessonId { get; private set; }
    public string Question { get; private set; } = string.Empty;

    /// <summary>JSON com array de strings representando as opções de resposta.</summary>
    public string OptionsJson { get; private set; } = "[]";

    public int CorrectIndex { get; private set; }
    public string Explanation { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    public Lesson? Lesson { get; private set; }
    public ICollection<QuizAttempt> Attempts { get; private set; } = [];

    private Quiz() { }

    public static Quiz Create(Guid lessonId, string question, string optionsJson, int correctIndex, string explanation)
    {
        return new Quiz
        {
            Id = Guid.NewGuid(),
            LessonId = lessonId,
            Question = question,
            OptionsJson = optionsJson,
            CorrectIndex = correctIndex,
            Explanation = explanation,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string question, string optionsJson, int correctIndex, string explanation)
    {
        Question = question;
        OptionsJson = optionsJson;
        CorrectIndex = correctIndex;
        Explanation = explanation;
    }
}
