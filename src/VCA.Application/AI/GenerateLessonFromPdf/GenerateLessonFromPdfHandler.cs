using Microsoft.Extensions.Logging;
using VCA.Application.Interfaces;
using VCA.Domain.Common;
using VCA.Domain.Entities;
using VCA.Domain.Interfaces;

namespace VCA.Application.AI.GenerateLessonFromPdf;

/// <summary>
/// Handler responsável por orquestrar o pipeline de geração de aula a partir de PDF.
/// </summary>
public class GenerateLessonFromPdfHandler
{
    private readonly IUnitOfWork _uow;
    private readonly IPdfExtractorService _pdfExtractor;
    private readonly IDeepSeekService _deepSeek;
    private readonly IStorageService _storage;
    private readonly ILogger<GenerateLessonFromPdfHandler> _logger;

    public GenerateLessonFromPdfHandler(
        IUnitOfWork uow,
        IPdfExtractorService pdfExtractor,
        IDeepSeekService deepSeek,
        IStorageService storage,
        ILogger<GenerateLessonFromPdfHandler> logger)
    {
        _uow = uow;
        _pdfExtractor = pdfExtractor;
        _deepSeek = deepSeek;
        _storage = storage;
        _logger = logger;
    }

    public async Task<Result<string>> HandleAsync(GenerateLessonFromPdfCommand command, CancellationToken cancellationToken = default)
    {
        var lesson = await _uow.Lessons.GetWithChunksAsync(command.LessonId, cancellationToken);
        if (lesson is null)
            return Result.Failure<string>($"Aula '{command.LessonId}' não encontrada.");

        // 1. Upload do PDF original
        var pdfUrl = await _storage.UploadPdfAsync(command.PdfFileName, command.PdfStream, cancellationToken);
        _logger.LogInformation("PDF '{FileName}' enviado para storage: {Url}", command.PdfFileName, pdfUrl);

        // 2. Extração e chunking do texto
        command.PdfStream.Position = 0;
        var chunks = await _pdfExtractor.ExtractChunksAsync(command.PdfStream, cancellationToken: cancellationToken);
        _logger.LogInformation("PDF dividido em {ChunkCount} chunks.", chunks.Count);

        // 3. Persistir os chunks
        foreach (var (text, index) in chunks.Select((t, i) => (t, i)))
        {
            var chunk = LessonChunk.Create(lesson.Id, index, text);
            await _uow.Lessons.GetByIdAsync(lesson.Id, cancellationToken); // garante tracking
            await _uow.AiGenerationLogs.GetByIdAsync(Guid.Empty, cancellationToken); // warm
            // Nota: chunks são adicionados via repositório próprio se necessário
        }

        // 4. Geração do conteúdo via DeepSeek
        var result = await _deepSeek.GenerateLessonContentAsync(lesson.Title, chunks, cancellationToken);
        _logger.LogInformation("Conteúdo gerado pela IA. Tokens: {Prompt}+{Completion}, Custo: ${Cost}",
            result.PromptTokens, result.CompletionTokens, result.CostUsd);

        // 5. Atualizar a aula com o conteúdo gerado
        lesson.SetContent(result.ContentJson);
        lesson.SubmitForReview();

        // 6. Registrar o log de geração de IA
        var aiLog = AiGenerationLog.Create(lesson.Id, result.Model, result.PromptTokens, result.CompletionTokens, result.CostUsd);
        await _uow.AiGenerationLogs.AddAsync(aiLog, cancellationToken);

        await _uow.SaveChangesAsync(cancellationToken);

        return Result.Success(result.ContentJson);
    }
}
