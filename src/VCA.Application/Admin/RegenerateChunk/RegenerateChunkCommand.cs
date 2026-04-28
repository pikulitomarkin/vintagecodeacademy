using MediatR;
using Microsoft.Extensions.Logging;
using VCA.Application.AI.Common;
using VCA.Application.Interfaces;
using VCA.Domain.Common;
using VCA.Domain.Entities;
using VCA.Domain.Enums;
using VCA.Domain.Interfaces;
using VCA.Domain.ValueObjects;

namespace VCA.Application.Admin.RegenerateChunk;

/// <summary>
/// Re-executa a geração de IA para um único chunk e mescla o conteúdo no JSON da aula.
/// O conteúdo dos demais chunks é preservado.
/// </summary>
public sealed record RegenerateChunkCommand(
    Guid LessonId,
    int ChunkIndex,
    DifficultyLevel Difficulty = DifficultyLevel.Intermediate,
    string Stack = "csharp"
) : IRequest<Result<decimal>>;

public sealed class RegenerateChunkHandler : IRequestHandler<RegenerateChunkCommand, Result<decimal>>
{
    private readonly IUnitOfWork _uow;
    private readonly IAiContentGenerator _generator;
    private readonly ILogger<RegenerateChunkHandler> _logger;

    public RegenerateChunkHandler(
        IUnitOfWork uow,
        IAiContentGenerator generator,
        ILogger<RegenerateChunkHandler> logger)
    {
        _uow = uow;
        _generator = generator;
        _logger = logger;
    }

    public async Task<Result<decimal>> Handle(RegenerateChunkCommand request, CancellationToken cancellationToken)
    {
        var lesson = await _uow.Lessons.GetWithChunksAsync(request.LessonId, cancellationToken);
        if (lesson is null)
            return Result.Failure<decimal>($"Aula '{request.LessonId}' não encontrada.");

        var chunk = lesson.Chunks.FirstOrDefault(c => c.ChunkIndex == request.ChunkIndex);
        if (chunk is null)
            return Result.Failure<decimal>($"Chunk {request.ChunkIndex} não encontrado para a aula.");

        var pdfChunk = new PdfChunk(chunk.ChunkIndex, chunk.RawText, EstimatedTokenCount: chunk.RawText.Length / 4);

        try
        {
            var result = await _generator.GenerateLessonAsync(
                new LessonGenerationRequest(lesson.Title, pdfChunk, request.Difficulty, request.Stack),
                cancellationToken);

            // Tenta carregar conteúdo atual; se inválido, usa o resultado novo direto.
            LessonContent? current = null;
            try { current = LessonContent.FromJson(lesson.ContentJson); }
            catch { /* ignore - usar somente o novo */ }

            var merged = current is null
                ? result.Content
                : new LessonContent(
                    Mission: current.Mission,
                    RealContext: current.RealContext,
                    Concept: $"{current.Concept}\n\n{result.Content.Concept}",
                    QuickChallenge: current.QuickChallenge,
                    Example: current.Example,
                    Summary: $"{current.Summary} {result.Content.Summary}".Trim(),
                    XpReward: current.XpReward);

            lesson.SetContent(merged.ToJson());

            var log = AiGenerationLog.Create(
                lesson.Id, result.Model, result.PromptTokens, result.CompletionTokens, result.CostUsd);
            await _uow.AiGenerationLogs.AddAsync(log, cancellationToken);

            _uow.Lessons.Update(lesson);
            await _uow.SaveChangesAsync(cancellationToken);

            return Result.Success(result.CostUsd);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Falha ao regenerar chunk {Index} da aula {LessonId}.", request.ChunkIndex, lesson.Id);
            return Result.Failure<decimal>($"Falha ao regenerar chunk: {ex.Message}");
        }
    }
}
