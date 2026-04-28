using MediatR;
using VCA.Domain.Enums;
using VCA.Domain.Interfaces;

namespace VCA.Application.AI.GetLessonDrafts;

public sealed class GetLessonDraftsHandler : IRequestHandler<GetLessonDraftsQuery, LessonDraftsPage>
{
    private readonly IUnitOfWork _uow;

    public GetLessonDraftsHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<LessonDraftsPage> Handle(GetLessonDraftsQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 100);

        var drafts = await _uow.Lessons.GetByStatusAsync(LessonStatus.Draft, cancellationToken);
        var pending = await _uow.Lessons.GetByStatusAsync(LessonStatus.PendingReview, cancellationToken);

        var combined = drafts.Concat(pending)
            .OrderByDescending(l => l.CreatedAt)
            .ToList();

        var totalCount = combined.Count;
        var paged = combined.Skip((page - 1) * size).Take(size).ToList();

        var items = new List<LessonDraftItem>(paged.Count);
        foreach (var l in paged)
        {
            var quizzes = await _uow.Quizzes.GetByLessonAsync(l.Id, cancellationToken);
            var totalCost = await _uow.AiGenerationLogs.GetTotalCostByLessonAsync(l.Id, cancellationToken);

            items.Add(new LessonDraftItem(
                l.Id, l.ModuleId, l.Title, l.Status.ToString(),
                ChunksProcessed: l.Chunks.Count,
                QuizCount: quizzes.Count,
                CreatedAt: l.CreatedAt,
                TotalCostUsd: totalCost));
        }

        return new LessonDraftsPage(items, page, size, totalCount);
    }
}
