using VCA.Domain.Enums;
using VCA.Domain.ValueObjects;

namespace VCA.Application.AI.Common;

/// <summary>
/// Solicitação de geração de aula para um chunk de PDF.
/// </summary>
public sealed record LessonGenerationRequest(
    string LessonTitle,
    PdfChunk Chunk,
    DifficultyLevel Difficulty,
    string Stack);

/// <summary>
/// Resultado de geração de aula — conteúdo validado + custo da chamada.
/// </summary>
public sealed record LessonGenerationResult(
    LessonContent Content,
    string Model,
    int PromptTokens,
    int CompletionTokens,
    decimal CostUsd,
    string PromptVersion,
    TimeSpan Duration);

/// <summary>
/// Solicitação de geração de quiz a partir do conteúdo de uma aula.
/// </summary>
public sealed record QuizGenerationRequest(
    string LessonTitle,
    LessonContent LessonContent,
    int QuestionCount = 10);

/// <summary>
/// Resultado de geração de quiz — questões validadas + custo da chamada.
/// </summary>
public sealed record QuizGenerationResult(
    IReadOnlyList<QuizQuestion> Questions,
    string Model,
    int PromptTokens,
    int CompletionTokens,
    decimal CostUsd,
    string PromptVersion,
    TimeSpan Duration);

/// <summary>
/// Resposta crua da API de IA (texto + custo).
/// </summary>
public sealed record AiCompletionResult(
    string Content,
    string Model,
    int PromptTokens,
    int CompletionTokens,
    decimal CostUsd,
    TimeSpan Duration);

/// <summary>
/// Resultado da geração completa de uma aula (lesson + quiz).
/// </summary>
public sealed record ContentGenerationResult(
    Guid LessonId,
    int ChunksProcessed,
    int ChunksFailed,
    int QuizzesGenerated,
    decimal TotalCostUsd,
    bool QuizGenerationFailed,
    string? QuizFailureReason);
