namespace VCA.Application.Admin.Common;

/// <summary>
/// DTOs compartilhados entre API e Blazor para o painel administrativo.
/// </summary>
public sealed record DashboardMetricsDto(
    int PublishedLessons,
    int DraftLessons,
    int PendingReviewLessons,
    int TotalQuizQuestions,
    decimal TotalAiCostUsd,
    decimal MonthlyAiCostUsd,
    double AverageQuizScore,
    int ActiveUsersLastWeek,
    IReadOnlyList<XpDayPointDto> XpDistributionLast7Days);

public sealed record XpDayPointDto(DateTime Day, int TotalXp);

public sealed record LessonDraftDetailDto(
    Guid LessonId,
    Guid ModuleId,
    string ModuleTitle,
    string Title,
    string Status,
    int XpReward,
    int Order,
    string ContentJson,
    DateTime CreatedAt,
    decimal TotalCostUsd,
    IReadOnlyList<DraftQuizDto> Quizzes,
    IReadOnlyList<DraftChunkDto> Chunks);

public sealed record DraftQuizDto(
    Guid Id,
    string Question,
    IReadOnlyList<string> Options,
    int CorrectIndex,
    string Explanation);

public sealed record DraftChunkDto(
    int ChunkIndex,
    string RawText);

public sealed record UpdateLessonContentRequest(
    string ContentJson,
    int XpReward,
    string Title,
    IReadOnlyList<UpdatedQuizDto>? Quizzes);

public sealed record UpdatedQuizDto(
    Guid Id,
    string Question,
    IReadOnlyList<string> Options,
    int CorrectIndex,
    string Explanation);

public sealed record ProcessPdfProgressEventDto(
    string Stage,
    int Current,
    int Total,
    string? Message,
    bool IsFinal,
    bool IsError,
    object? Result);
