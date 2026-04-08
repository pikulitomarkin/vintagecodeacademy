namespace VCA.Domain.Entities;

/// <summary>
/// Log de chamadas à API de IA (DeepSeek) para geração de aulas e quizzes.
/// Usado para monitoramento de custo e rastreabilidade.
/// </summary>
public class AiGenerationLog
{
    public Guid Id { get; private set; }
    public Guid LessonId { get; private set; }
    public string Model { get; private set; } = string.Empty;
    public int PromptTokens { get; private set; }
    public int CompletionTokens { get; private set; }
    public decimal CostUsd { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public Lesson? Lesson { get; private set; }

    private AiGenerationLog() { }

    public static AiGenerationLog Create(Guid lessonId, string model, int promptTokens, int completionTokens, decimal costUsd)
    {
        return new AiGenerationLog
        {
            Id = Guid.NewGuid(),
            LessonId = lessonId,
            Model = model,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            CostUsd = costUsd,
            CreatedAt = DateTime.UtcNow
        };
    }
}
