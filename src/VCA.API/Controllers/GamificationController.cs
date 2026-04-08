using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VCA.Application.Gamification.CompleteLesson;
using VCA.Application.Gamification.SubmitQuiz;

namespace VCA.API.Controllers;

/// <summary>
/// Endpoints de gamificação: conclusão de aulas e submissão de quizzes.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GamificationController : ControllerBase
{
    private readonly CompleteLessonHandler _completeLessonHandler;
    private readonly SubmitQuizHandler _submitQuizHandler;

    public GamificationController(CompleteLessonHandler completeLessonHandler, SubmitQuizHandler submitQuizHandler)
    {
        _completeLessonHandler = completeLessonHandler;
        _submitQuizHandler = submitQuizHandler;
    }

    private Guid GetCurrentUserId()
    {
        if (HttpContext.Items["UserId"] is Guid userId) return userId;
        throw new UnauthorizedAccessException("Usuário não autenticado.");
    }

    /// <summary>Registra a conclusão de uma aula e concede XP ao usuário.</summary>
    [HttpPost("lessons/{lessonId:guid}/complete")]
    public async Task<IActionResult> CompleteLesson(Guid lessonId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _completeLessonHandler.HandleAsync(new CompleteLessonCommand(userId, lessonId), cancellationToken);

        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }

    /// <summary>Submete as respostas de um quiz e calcula a pontuação.</summary>
    [HttpPost("lessons/{lessonId:guid}/quiz")]
    public async Task<IActionResult> SubmitQuiz(Guid lessonId, [FromBody] SubmitQuizRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var command = new SubmitQuizCommand(userId, lessonId, request.SelectedAnswers);
        var result = await _submitQuizHandler.HandleAsync(command, cancellationToken);

        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        return Ok(result.Value);
    }
}

public record SubmitQuizRequest(List<int> SelectedAnswers);
