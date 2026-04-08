namespace VCA.Application.Interfaces;

/// <summary>
/// Contrato para integração com a API DeepSeek (geração de conteúdo de aulas e quizzes).
/// </summary>
public interface IDeepSeekService
{
    /// <summary>
    /// Gera o conteúdo JSON gamificado de uma aula a partir de chunks de texto.
    /// </summary>
    Task<DeepSeekGenerationResult> GenerateLessonContentAsync(
        string lessonTitle,
        IEnumerable<string> textChunks,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gera questões de quiz a partir do conteúdo de uma aula.
    /// </summary>
    Task<DeepSeekGenerationResult> GenerateQuizQuestionsAsync(
        string lessonTitle,
        string lessonContentJson,
        int questionCount = 10,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Resultado de uma chamada à API DeepSeek.
/// </summary>
public record DeepSeekGenerationResult(
    string ContentJson,
    string Model,
    int PromptTokens,
    int CompletionTokens,
    decimal CostUsd
);
