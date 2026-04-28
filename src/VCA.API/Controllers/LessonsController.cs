using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using VCA.API.Extensions;
using VCA.Application.Gamification;
using VCA.Application.Gamification.Commands;
using VCA.Application.Gamification.CompleteLesson;
using VCA.Domain.Interfaces;

namespace VCA.API.Controllers;

/// <summary>
/// Endpoints para aulas e conclusão de desafios rápidos.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LessonsController : ControllerBase
{
    private readonly IUnitOfWork _uow;
    private readonly CompleteLessonHandler _completeLessonHandler;
    private readonly ISender _sender;

    public LessonsController(
        IUnitOfWork uow,
        CompleteLessonHandler completeLessonHandler,
        ISender sender)
    {
        _uow = uow;
        _completeLessonHandler = completeLessonHandler;
        _sender = sender;
    }

    /// <summary>Retorna os detalhes completos de uma aula com o conteúdo gamificado deserializado.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(LessonDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [SwaggerOperation(Summary = "Detalhes de uma aula com conteúdo gamificado")]
    public async Task<ActionResult<LessonDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var lesson = await _uow.Lessons.GetByIdAsync(id, cancellationToken);
        if (lesson is null) return NotFound();

        var userId = HttpContext.TryGetUserId();
        bool completed = false;

        if (userId.HasValue)
            completed = await _uow.UserProgresses.HasCompletedAsync(userId.Value, id, cancellationToken);

        return Ok(new LessonDetailDto(
            lesson.Id,
            lesson.Title,
            lesson.ContentJson,
            lesson.XpReward,
            lesson.Order,
            lesson.Status.ToString(),
            completed));
    }

    /// <summary>
    /// Marca a aula como concluída e concede XP ao usuário.
    /// Retorna erro se a aula já foi concluída anteriormente.
    /// </summary>
    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType(typeof(CompleteLessonResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [SwaggerOperation(Summary = "Conclui uma aula e concede XP")]
    public async Task<ActionResult<CompleteLessonResultDto>> CompleteLesson(Guid id, CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetUserId();
        var result = await _completeLessonHandler.HandleAsync(
            new CompleteLessonCommand(userId, id), cancellationToken);

        if (result.IsFailure)
            return BadRequest(new ErrorResponse(result.Error!));

        var v = result.Value!;
        return Ok(new CompleteLessonResultDto(v.XpEarned, v.TotalXp, v.NewLevel.ToString()));
    }

    /// <summary>
    /// Registra a conclusão de um desafio rápido embutido na aula e concede XP (+15).
    /// Pode ser chamado independentemente da conclusão da aula.
    /// </summary>
    [HttpPost("{id:guid}/quick-challenge")]
    [ProducesResponseType(typeof(XpAwardedDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [SwaggerOperation(Summary = "Submete desafio rápido e concede XP (+15)")]
    public async Task<ActionResult<XpAwardedDto>> QuickChallenge(Guid id, CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetUserId();
        var result = await _sender.Send(
            new AwardXpCommand(userId, XpReason.QuickChallengeCompleted, id),
            cancellationToken);

        if (result.IsFailure)
            return BadRequest(new ErrorResponse(result.Error!));

        var v = result.Value!;
        return Ok(new XpAwardedDto(v.XpAwarded, v.NewTotalXp, v.NewLevel));
    }
}

public record LessonDetailDto(Guid Id, string Title, string ContentJson, int XpReward, int Order, string Status, bool CompletedByUser);
public record CompleteLessonResultDto(int XpEarned, int TotalXp, string NewLevel);
public record XpAwardedDto(int XpAwarded, int NewTotalXp, string? NewLevel);
