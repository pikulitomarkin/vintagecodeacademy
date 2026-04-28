using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using VCA.API.Extensions;
using VCA.Application.Courses.GetTrails;
using VCA.Domain.Interfaces;

namespace VCA.API.Controllers;

/// <summary>
/// Endpoints para trilhas de aprendizado.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TrailsController : ControllerBase
{
    private readonly GetTrailsHandler _handler;
    private readonly IUnitOfWork _uow;

    public TrailsController(GetTrailsHandler handler, IUnitOfWork uow)
    {
        _handler = handler;
        _uow = uow;
    }

    /// <summary>Lista todas as trilhas publicadas.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<TrailSummaryDto>), StatusCodes.Status200OK)]
    [SwaggerOperation(Summary = "Lista trilhas publicadas")]
    public async Task<ActionResult<IReadOnlyList<TrailSummaryDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _handler.HandleAsync(new GetTrailsQuery(PublishedOnly: true), cancellationToken);

        var dtos = result.Value!.Select(t => new TrailSummaryDto(
            t.Id, t.Title, t.Description, t.Stack, t.Level.ToString(), t.Order,
            t.Modules.Count)).ToList();

        return Ok(dtos);
    }

    /// <summary>Retorna detalhes de uma trilha com seus módulos e aulas.</summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TrailDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [SwaggerOperation(Summary = "Detalhes de uma trilha com módulos")]
    public async Task<ActionResult<TrailDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var trail = await _uow.Trails.GetWithModulesAsync(id, cancellationToken);
        if (trail is null) return NotFound();

        var moduleDtos = trail.Modules
            .OrderBy(m => m.Order)
            .Select(m => new ModuleSummaryDto(
                m.Id, m.Title, m.Order,
                m.Lessons.OrderBy(l => l.Order)
                    .Select(l => new LessonSummaryDto(l.Id, l.Title, l.Order, l.XpReward, l.Status.ToString()))
                    .ToList()))
            .ToList();

        return Ok(new TrailDetailDto(
            trail.Id, trail.Title, trail.Description,
            trail.Stack, trail.Level.ToString(), trail.Order, moduleDtos));
    }

    /// <summary>Retorna o progresso do usuário autenticado em uma trilha específica.</summary>
    [HttpGet("{id:guid}/progress")]
    [ProducesResponseType(typeof(TrailProgressDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [SwaggerOperation(Summary = "Progresso do usuário na trilha")]
    public async Task<ActionResult<TrailProgressDto>> GetProgress(Guid id, CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetUserId();

        var trail = await _uow.Trails.GetWithModulesAsync(id, cancellationToken);
        if (trail is null) return NotFound();

        var userProgress = await _uow.UserProgresses.GetByUserAsync(userId, cancellationToken);
        var completedLessonIds = userProgress.Select(p => p.LessonId).ToHashSet();

        var totalLessons = trail.Modules.SelectMany(m => m.Lessons).Count();
        var completedCount = trail.Modules
            .SelectMany(m => m.Lessons)
            .Count(l => completedLessonIds.Contains(l.Id));

        var totalXpEarned = userProgress
            .Where(p => trail.Modules
                .SelectMany(m => m.Lessons)
                .Any(l => l.Id == p.LessonId))
            .Sum(p => p.XpEarned);

        return Ok(new TrailProgressDto(
            trail.Id, trail.Title,
            completedCount, totalLessons,
            totalLessons > 0 ? completedCount * 100 / totalLessons : 0,
            totalXpEarned));
    }
}

public record TrailSummaryDto(Guid Id, string Title, string Description, string Stack, string Level, int Order, int TotalModules);
public record TrailDetailDto(Guid Id, string Title, string Description, string Stack, string Level, int Order, List<ModuleSummaryDto> Modules);
public record ModuleSummaryDto(Guid Id, string Title, int Order, List<LessonSummaryDto> Lessons);
public record LessonSummaryDto(Guid Id, string Title, int Order, int XpReward, string Status);
public record TrailProgressDto(Guid TrailId, string TrailTitle, int CompletedLessons, int TotalLessons, int ProgressPercent, int TotalXpEarned);
