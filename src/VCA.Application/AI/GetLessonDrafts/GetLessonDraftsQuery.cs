using MediatR;

namespace VCA.Application.AI.GetLessonDrafts;

public sealed record GetLessonDraftsQuery(int Page = 1, int PageSize = 20) : IRequest<LessonDraftsPage>;

public sealed record LessonDraftItem(
    Guid LessonId,
    Guid ModuleId,
    string Title,
    string Status,
    int ChunksProcessed,
    int QuizCount,
    DateTime CreatedAt,
    decimal TotalCostUsd);

public sealed record LessonDraftsPage(
    IReadOnlyList<LessonDraftItem> Items,
    int Page,
    int PageSize,
    int TotalCount);
