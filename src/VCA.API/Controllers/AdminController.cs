using System.Text.Json;
using System.Threading.Channels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using VCA.Application.Admin.Common;
using VCA.Application.Admin.Dashboard;
using VCA.Application.Admin.GetLessonDraftDetail;
using VCA.Application.Admin.RegenerateChunk;
using VCA.Application.Admin.UpdateLessonContent;
using VCA.Application.AI.GetLessonDrafts;
using VCA.Application.AI.ProcessLessonPdf;
using VCA.Application.AI.PublishLesson;
using VCA.Domain.Enums;
using VCA.Domain.Interfaces;

namespace VCA.API.Controllers;

/// <summary>
/// Endpoints administrativos — revisão e publicação de aulas geradas por IA.
/// Acesso restrito à role "Admin".
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IUnitOfWork _uow;
    private readonly ISender _mediator;

    public AdminController(IUnitOfWork uow, ISender mediator)
    {
        _uow = uow;
        _mediator = mediator;
    }

    /// <summary>
    /// Lista paginada de aulas em Draft ou PendingReview.
    /// </summary>
    [HttpGet("lessons/drafts")]
    [ProducesResponseType(typeof(LessonDraftsPage), StatusCodes.Status200OK)]
    [SwaggerOperation(Summary = "Lista rascunhos e aulas pendentes de revisão (paginada)")]
    public async Task<ActionResult<LessonDraftsPage>> GetDrafts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetLessonDraftsQuery(page, pageSize), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Processa um PDF e gera o conteúdo gamificado + pool de quiz para uma aula existente.
    /// Body: multipart/form-data com o arquivo PDF.
    /// </summary>
    [HttpPost("lessons/{id:guid}/process-pdf")]
    [RequestSizeLimit(60 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [SwaggerOperation(Summary = "Gera conteúdo de uma aula a partir de um PDF (Admin)")]
    public async Task<IActionResult> ProcessPdf(
        Guid id,
        [FromForm] IFormFile pdf,
        [FromForm] DifficultyLevel difficulty = DifficultyLevel.Intermediate,
        [FromForm] string stack = "csharp",
        [FromForm] bool generateQuiz = true,
        [FromForm] int quizQuestionCount = 10,
        CancellationToken cancellationToken = default)
    {
        if (pdf is null || pdf.Length == 0)
            return BadRequest(new ErrorResponse("Arquivo PDF é obrigatório."));

        await using var stream = pdf.OpenReadStream();
        var command = new ProcessLessonPdfCommand(
            id, stream, pdf.FileName, difficulty, stack, generateQuiz, quizQuestionCount);

        var result = await _mediator.Send(command, cancellationToken);
        if (result.IsFailure)
            return BadRequest(new ErrorResponse(result.Error ?? "Falha desconhecida no pipeline."));

        return Ok(result.Value);
    }

    /// <summary>
    /// Publica uma aula — valida conteúdo + pool de quiz antes de marcar como Published.
    /// </summary>
    [HttpPut("lessons/{id:guid}/publish")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [SwaggerOperation(Summary = "Publica uma aula (Admin)")]
    public async Task<IActionResult> Publish(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new PublishLessonCommand(id), cancellationToken);
        if (result.IsFailure)
        {
            if (result.Error?.Contains("não encontrada", StringComparison.OrdinalIgnoreCase) == true)
                return NotFound();
            return BadRequest(new ErrorResponse(result.Error ?? "Falha ao publicar aula."));
        }
        return NoContent();
    }

    /// <summary>
    /// Arquiva uma aula — altera status para Archived e a torna invisível para os alunos.
    /// </summary>
    [HttpPut("lessons/{id:guid}/archive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [SwaggerOperation(Summary = "Arquiva uma aula (Admin)")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        var lesson = await _uow.Lessons.GetByIdAsync(id, cancellationToken);
        if (lesson is null) return NotFound();

        if (lesson.Status == LessonStatus.Archived)
            return BadRequest(new ErrorResponse("A aula já está arquivada."));

        lesson.Archive();
        await _uow.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Métricas globais do painel administrativo (lições, custo IA, quizzes, XP/dia).
    /// </summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(DashboardMetricsDto), StatusCodes.Status200OK)]
    [SwaggerOperation(Summary = "Métricas globais do painel admin")]
    public async Task<ActionResult<DashboardMetricsDto>> GetDashboard(CancellationToken cancellationToken)
    {
        var metrics = await _mediator.Send(new GetDashboardMetricsQuery(), cancellationToken);
        return Ok(metrics);
    }

    /// <summary>
    /// Detalhe completo de um draft — conteúdo JSON, quizzes e chunks.
    /// </summary>
    [HttpGet("lessons/{id:guid}/draft-detail")]
    [ProducesResponseType(typeof(LessonDraftDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [SwaggerOperation(Summary = "Detalhe de um draft para revisão (Admin)")]
    public async Task<IActionResult> GetDraftDetail(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetLessonDraftDetailQuery(id), cancellationToken);
        if (result.IsFailure)
            return NotFound(new ErrorResponse(result.Error ?? "Aula não encontrada."));
        return Ok(result.Value);
    }

    /// <summary>
    /// Atualiza o conteúdo (JSON), título, XP e quizzes de uma aula em revisão.
    /// </summary>
    [HttpPut("lessons/{id:guid}/content")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [SwaggerOperation(Summary = "Atualiza conteúdo de uma aula (Admin)")]
    public async Task<IActionResult> UpdateContent(
        Guid id,
        [FromBody] UpdateLessonContentRequest body,
        CancellationToken cancellationToken)
    {
        if (body is null)
            return BadRequest(new ErrorResponse("Corpo da requisição é obrigatório."));

        var result = await _mediator.Send(
            new UpdateLessonContentCommand(id, body.ContentJson, body.XpReward, body.Title, body.Quizzes),
            cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error?.Contains("não encontrada", StringComparison.OrdinalIgnoreCase) == true)
                return NotFound(new ErrorResponse(result.Error));
            return BadRequest(new ErrorResponse(result.Error ?? "Falha ao atualizar conteúdo."));
        }
        return NoContent();
    }

    /// <summary>
    /// Re-executa a IA para um chunk específico, mantendo o resto da aula inalterado.
    /// </summary>
    [HttpPost("lessons/{id:guid}/regenerate-chunk/{chunkIndex:int}")]
    [ProducesResponseType(typeof(RegenerateChunkResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [SwaggerOperation(Summary = "Regenera um chunk específico (Admin)")]
    public async Task<IActionResult> RegenerateChunk(
        Guid id,
        int chunkIndex,
        [FromQuery] DifficultyLevel difficulty = DifficultyLevel.Intermediate,
        [FromQuery] string stack = "csharp",
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new RegenerateChunkCommand(id, chunkIndex, difficulty, stack),
            cancellationToken);

        if (result.IsFailure)
            return BadRequest(new ErrorResponse(result.Error ?? "Falha ao regenerar chunk."));

        return Ok(new RegenerateChunkResponse(result.Value));
    }

    /// <summary>
    /// Processa um PDF gerando conteúdo + quiz, com progresso em tempo real via SSE.
    /// O cliente deve enviar `Accept: text/event-stream`.
    /// Cada evento: `data: {ProcessPdfProgressEventDto JSON}\n\n`. O último evento tem `IsFinal=true`.
    /// </summary>
    [HttpPost("lessons/{id:guid}/process-pdf-stream")]
    [RequestSizeLimit(60 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    [Produces("text/event-stream")]
    [SwaggerOperation(Summary = "Processa PDF com progresso SSE (Admin)")]
    public async Task ProcessPdfStream(
        Guid id,
        [FromForm] IFormFile pdf,
        [FromForm] DifficultyLevel difficulty = DifficultyLevel.Intermediate,
        [FromForm] string stack = "csharp",
        [FromForm] bool generateQuiz = true,
        [FromForm] int quizQuestionCount = 10,
        CancellationToken cancellationToken = default)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        if (pdf is null || pdf.Length == 0)
        {
            await WriteEventAsync(new ProcessPdfProgressEventDto(
                "error", 0, 0, "Arquivo PDF é obrigatório.", IsFinal: true, IsError: true, Result: null),
                cancellationToken);
            return;
        }

        var channel = Channel.CreateUnbounded<ProcessLessonPdfProgress>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        var progress = new Progress<ProcessLessonPdfProgress>(p =>
        {
            channel.Writer.TryWrite(p);
        });

        await using var stream = pdf.OpenReadStream();
        var command = new ProcessLessonPdfCommand(
            id, stream, pdf.FileName, difficulty, stack, generateQuiz, quizQuestionCount, progress);

        var pipelineTask = Task.Run(async () =>
        {
            try
            {
                return await _mediator.Send(command, cancellationToken);
            }
            finally
            {
                channel.Writer.TryComplete();
            }
        }, cancellationToken);

        // Drena progresso até o pipeline finalizar.
        await foreach (var p in channel.Reader.ReadAllAsync(cancellationToken))
        {
            await WriteEventAsync(new ProcessPdfProgressEventDto(
                p.Stage, p.Current, p.Total, p.Message, IsFinal: false, IsError: false, Result: null),
                cancellationToken);
        }

        var pipelineResult = await pipelineTask;
        if (pipelineResult.IsFailure)
        {
            await WriteEventAsync(new ProcessPdfProgressEventDto(
                "error", 0, 0, pipelineResult.Error, IsFinal: true, IsError: true, Result: null),
                cancellationToken);
        }
        else
        {
            await WriteEventAsync(new ProcessPdfProgressEventDto(
                "completed", 1, 1, "Pipeline concluído.", IsFinal: true, IsError: false, Result: pipelineResult.Value),
                cancellationToken);
        }
    }

    private async Task WriteEventAsync(ProcessPdfProgressEventDto evt, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(evt, _sseJsonOptions);
        await Response.WriteAsync($"data: {json}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }

    private static readonly JsonSerializerOptions _sseJsonOptions = new(JsonSerializerDefaults.Web);
}

public record AdminLessonDto(
    Guid Id, Guid ModuleId, string Title, string Status,
    int XpReward, int Order, DateTime CreatedAt);

public record RegenerateChunkResponse(decimal CostUsd);
