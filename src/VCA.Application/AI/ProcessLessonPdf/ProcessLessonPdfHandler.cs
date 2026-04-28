using MediatR;
using Microsoft.Extensions.Logging;
using VCA.Application.AI.Common;
using VCA.Application.AI.Services;
using VCA.Application.Interfaces;
using VCA.Domain.Common;
using VCA.Domain.Entities;
using VCA.Domain.Interfaces;
using VCA.Domain.ValueObjects;

namespace VCA.Application.AI.ProcessLessonPdf;

/// <summary>
/// Handler do pipeline de geração de aula a partir de PDF.
/// Robustez:
///   - Falha em chunk individual não derruba o pipeline (best-effort, logs em AiGenerationLog).
///   - Falha de quiz não rola back o conteúdo da aula.
///   - Storage upload é best-effort (não fatal).
/// </summary>
public sealed class ProcessLessonPdfHandler : IRequestHandler<ProcessLessonPdfCommand, Result<ContentGenerationResult>>
{
    private readonly IUnitOfWork _uow;
    private readonly PdfIngestionService _ingestion;
    private readonly IAiContentGenerator _generator;
    private readonly IStorageService _storage;
    private readonly ILogger<ProcessLessonPdfHandler> _logger;

    public ProcessLessonPdfHandler(
        IUnitOfWork uow,
        PdfIngestionService ingestion,
        IAiContentGenerator generator,
        IStorageService storage,
        ILogger<ProcessLessonPdfHandler> logger)
    {
        _uow = uow;
        _ingestion = ingestion;
        _generator = generator;
        _storage = storage;
        _logger = logger;
    }

    public async Task<Result<ContentGenerationResult>> Handle(
        ProcessLessonPdfCommand request,
        CancellationToken cancellationToken)
    {
        var lesson = await _uow.Lessons.GetByIdAsync(request.LessonId, cancellationToken);
        if (lesson is null)
            return Result.Failure<ContentGenerationResult>($"Aula '{request.LessonId}' não encontrada.");

        Report(request, "starting", 0, 1, $"Aula: {lesson.Title}");

        // 1. Upload PDF (best-effort)
        try
        {
            if (request.PdfStream.CanSeek) request.PdfStream.Position = 0;
            var url = await _storage.UploadPdfAsync(request.FileName, request.PdfStream, cancellationToken);
            _logger.LogInformation("PDF '{File}' enviado: {Url}", request.FileName, url);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao fazer upload do PDF (não fatal): {File}", request.FileName);
        }

        // 2. Ingestão (parsing + chunking)
        if (request.PdfStream.CanSeek) request.PdfStream.Position = 0;
        PdfIngestionResult ingestion;
        try
        {
            ingestion = await _ingestion.IngestAsync(request.PdfStream, request.FileName, cancellationToken);
        }
        catch (DomainException ex)
        {
            _logger.LogError(ex, "Ingestão de PDF falhou: code={Code}", ex.Code);
            return Result.Failure<ContentGenerationResult>($"Falha na ingestão do PDF: {ex.Message}");
        }

        if (ingestion.Chunks.Count == 0)
            return Result.Failure<ContentGenerationResult>("Nenhum chunk gerado a partir do PDF.");

        Report(request, "chunked", 0, ingestion.Chunks.Count, $"{ingestion.Chunks.Count} chunks gerados.");

        // 3. Persistir chunks (LessonChunk) — antes da IA, para auditoria
        foreach (var c in ingestion.Chunks)
        {
            var chunkEntity = LessonChunk.Create(lesson.Id, c.ChunkIndex, c.RawText);
            lesson.Chunks.Add(chunkEntity);
        }

        // 4. Gerar conteúdo por chunk (best-effort por chunk)
        LessonContent? aggregated = null;
        int processed = 0, failed = 0;
        decimal totalCost = 0m;

        for (int i = 0; i < ingestion.Chunks.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var chunk = ingestion.Chunks[i];

            Report(request, "generating_lesson", i + 1, ingestion.Chunks.Count,
                $"Chunk {i + 1}/{ingestion.Chunks.Count} (≈{chunk.EstimatedTokenCount} tokens)");

            try
            {
                var result = await _generator.GenerateLessonAsync(
                    new LessonGenerationRequest(lesson.Title, chunk, request.Difficulty, request.Stack),
                    cancellationToken);

                aggregated = aggregated is null
                    ? result.Content
                    : Merge(aggregated, result.Content);

                totalCost += result.CostUsd;
                processed++;

                var log = AiGenerationLog.Create(
                    lesson.Id, result.Model, result.PromptTokens, result.CompletionTokens, result.CostUsd);
                await _uow.AiGenerationLogs.AddAsync(log, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
                _logger.LogError(ex,
                    "Falha ao gerar conteúdo do chunk {Index}/{Total} para aula {LessonId}.",
                    i + 1, ingestion.Chunks.Count, lesson.Id);
            }
        }

        if (aggregated is null)
            return Result.Failure<ContentGenerationResult>(
                $"Pipeline falhou em todos os {ingestion.Chunks.Count} chunks. Verifique logs.");

        // 5. Persistir conteúdo + status Draft
        lesson.SetContent(aggregated.ToJson());

        // 6. Quiz (best-effort, falha não rola back conteúdo)
        bool quizFailed = false;
        string? quizError = null;
        int quizCount = 0;

        if (request.GenerateQuiz)
        {
            Report(request, "generating_quiz", 0, 1, "Gerando pool de quiz...");
            try
            {
                var quizResult = await _generator.GenerateQuizAsync(
                    new QuizGenerationRequest(lesson.Title, aggregated, request.QuizQuestionCount),
                    cancellationToken);

                foreach (var q in quizResult.Questions)
                {
                    var quiz = Quiz.Create(lesson.Id, q.Question, q.OptionsToJson(), q.CorrectIndex, q.Explanation);
                    await _uow.Quizzes.AddAsync(quiz, cancellationToken);
                }
                quizCount = quizResult.Questions.Count;
                totalCost += quizResult.CostUsd;

                var quizLog = AiGenerationLog.Create(
                    lesson.Id, quizResult.Model,
                    quizResult.PromptTokens, quizResult.CompletionTokens, quizResult.CostUsd);
                await _uow.AiGenerationLogs.AddAsync(quizLog, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                quizFailed = true;
                quizError = ex.Message;
                _logger.LogError(ex,
                    "Falha ao gerar quiz para aula {LessonId}. Conteúdo da aula foi preservado.",
                    lesson.Id);
            }
        }

        lesson.SubmitForReview();
        await _uow.SaveChangesAsync(cancellationToken);

        Report(request, "completed", 1, 1,
            $"Processados: {processed}/{ingestion.Chunks.Count} chunks, quiz: {quizCount} questões. Custo: ${totalCost:F4}.");

        return Result.Success(new ContentGenerationResult(
            LessonId: lesson.Id,
            ChunksProcessed: processed,
            ChunksFailed: failed,
            QuizzesGenerated: quizCount,
            TotalCostUsd: totalCost,
            QuizGenerationFailed: quizFailed,
            QuizFailureReason: quizError));
    }

    private static void Report(ProcessLessonPdfCommand cmd, string stage, int current, int total, string? msg = null) =>
        cmd.Progress?.Report(new ProcessLessonPdfProgress(stage, current, total, msg));

    /// <summary>
    /// Combina conteúdos de múltiplos chunks: mantém missão/contexto/exemplo do primeiro chunk válido,
    /// concatena conceito e resumo, soma XP (limitado).
    /// </summary>
    private static LessonContent Merge(LessonContent a, LessonContent b)
    {
        return new LessonContent(
            Mission: a.Mission,
            RealContext: a.RealContext,
            Concept: $"{a.Concept}\n\n{b.Concept}",
            QuickChallenge: a.QuickChallenge,
            Example: a.Example,
            Summary: $"{a.Summary}\n{b.Summary}",
            XpReward: Math.Min(100, a.XpReward + (b.XpReward / 2)));
    }
}
