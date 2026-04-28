using System.Text.Json;
using MediatR;
using VCA.Application.Admin.Common;
using VCA.Domain.Common;
using VCA.Domain.Interfaces;

namespace VCA.Application.Admin.GetLessonDraftDetail;

public sealed class GetLessonDraftDetailHandler
    : IRequestHandler<GetLessonDraftDetailQuery, Result<LessonDraftDetailDto>>
{
    private readonly IUnitOfWork _uow;

    public GetLessonDraftDetailHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Result<LessonDraftDetailDto>> Handle(
        GetLessonDraftDetailQuery request,
        CancellationToken cancellationToken)
    {
        var lesson = await _uow.Lessons.GetWithChunksAsync(request.LessonId, cancellationToken);
        if (lesson is null)
            return Result.Failure<LessonDraftDetailDto>($"Aula '{request.LessonId}' não encontrada.");

        var module = await _uow.Modules.GetByIdAsync(lesson.ModuleId, cancellationToken);
        var quizzes = await _uow.Quizzes.GetByLessonAsync(lesson.Id, cancellationToken);
        var totalCost = await _uow.AiGenerationLogs.GetTotalCostByLessonAsync(lesson.Id, cancellationToken);

        var quizDtos = quizzes
            .Select(q => new DraftQuizDto(
                q.Id,
                q.Question,
                ParseOptions(q.OptionsJson),
                q.CorrectIndex,
                q.Explanation))
            .ToList();

        var chunkDtos = lesson.Chunks
            .OrderBy(c => c.ChunkIndex)
            .Select(c => new DraftChunkDto(c.ChunkIndex, c.RawText))
            .ToList();

        var dto = new LessonDraftDetailDto(
            LessonId: lesson.Id,
            ModuleId: lesson.ModuleId,
            ModuleTitle: module?.Title ?? "(módulo desconhecido)",
            Title: lesson.Title,
            Status: lesson.Status.ToString(),
            XpReward: lesson.XpReward,
            Order: lesson.Order,
            ContentJson: lesson.ContentJson,
            CreatedAt: lesson.CreatedAt,
            TotalCostUsd: totalCost,
            Quizzes: quizDtos,
            Chunks: chunkDtos);

        return Result.Success(dto);
    }

    private static IReadOnlyList<string> ParseOptions(string optionsJson)
    {
        if (string.IsNullOrWhiteSpace(optionsJson)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(optionsJson) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
