using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VCA.Application.AI.GenerateLessonFromPdf;
using VCA.Application.AI.GenerateQuiz;

namespace VCA.API.Controllers;

/// <summary>
/// Endpoints para geração de conteúdo via IA (acesso restrito a administradores).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AdminOnly")]
public class AiController : ControllerBase
{
    private readonly GenerateLessonFromPdfHandler _generateLessonHandler;
    private readonly GenerateQuizHandler _generateQuizHandler;

    public AiController(GenerateLessonFromPdfHandler generateLessonHandler, GenerateQuizHandler generateQuizHandler)
    {
        _generateLessonHandler = generateLessonHandler;
        _generateQuizHandler = generateQuizHandler;
    }

    /// <summary>
    /// Processa um PDF e gera o conteúdo gamificado de uma aula via DeepSeek.
    /// Fluxo: upload → chunking → IA → revisão admin → publicação.
    /// </summary>
    [HttpPost("lessons/{lessonId:guid}/generate-from-pdf")]
    [RequestSizeLimit(20 * 1024 * 1024)] // 20 MB
    public async Task<IActionResult> GenerateFromPdf(Guid lessonId, IFormFile pdf, CancellationToken cancellationToken)
    {
        if (pdf.ContentType != "application/pdf")
            return BadRequest(new { error = "O arquivo deve ser um PDF válido." });

        await using var stream = pdf.OpenReadStream();
        var command = new GenerateLessonFromPdfCommand(lessonId, stream, pdf.FileName);
        var result = await _generateLessonHandler.HandleAsync(command, cancellationToken);

        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        return Ok(new { message = "Conteúdo gerado com sucesso. Aguardando revisão do admin.", contentJson = result.Value });
    }

    /// <summary>Gera um pool de questões de quiz para uma aula via DeepSeek.</summary>
    [HttpPost("lessons/{lessonId:guid}/generate-quiz")]
    public async Task<IActionResult> GenerateQuiz(Guid lessonId, [FromQuery] int questionCount = 10, CancellationToken cancellationToken = default)
    {
        var result = await _generateQuizHandler.HandleAsync(new GenerateQuizCommand(lessonId, questionCount), cancellationToken);

        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        return Ok(new { questionsGenerated = result.Value });
    }
}
