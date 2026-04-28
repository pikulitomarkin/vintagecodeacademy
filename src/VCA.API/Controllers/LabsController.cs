using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using VCA.API.Extensions;
using VCA.Domain.Entities;
using VCA.Domain.Enums;
using VCA.Domain.Interfaces;

namespace VCA.API.Controllers;

/// <summary>
/// Endpoints do VCA Labs — projetos open-source reais para alunos de nível avançado.
/// Requer nível VintageDev (nível 5, XP ≥ 25.000) para candidatura e listagem.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LabsController : ControllerBase
{
    private readonly IUnitOfWork _uow;

    public LabsController(IUnitOfWork uow) => _uow = uow;

    /// <summary>
    /// Lista projetos abertos para candidatura.
    /// Acesso restrito a usuários de nível VintageDev (nível 5, XP ≥ 25.000).
    /// </summary>
    [HttpGet("projects")]
    [ProducesResponseType(typeof(List<LabProjectDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [SwaggerOperation(Summary = "Lista projetos Labs abertos (requer nível VintageDev)")]
    public async Task<ActionResult<List<LabProjectDto>>> GetProjects(CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetUserId();
        var forbidden = await RequireMinLevelAsync(userId, UserLevel.VintageDev, cancellationToken);
        if (forbidden is not null) return forbidden;

        var projects = await _uow.LabProjects.GetOpenProjectsAsync(cancellationToken);

        var dtos = projects.Select(p => new LabProjectDto(
            p.Id, p.Title, p.Description, p.Stack, p.Status, p.SlotsAvailable, p.CreatedAt)).ToList();

        return Ok(dtos);
    }

    /// <summary>
    /// Submete candidatura a um projeto Labs.
    /// Acesso restrito a usuários de nível VintageDev (nível 5, XP ≥ 25.000).
    /// </summary>
    [HttpPost("projects/{id:guid}/apply")]
    [ProducesResponseType(typeof(LabApplicationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [SwaggerOperation(Summary = "Candidatura a um projeto Labs (requer nível VintageDev)")]
    public async Task<ActionResult<LabApplicationDto>> Apply(Guid id, CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetUserId();
        var forbidden = await RequireMinLevelAsync(userId, UserLevel.VintageDev, cancellationToken);
        if (forbidden is not null) return forbidden;

        var project = await _uow.LabProjects.GetByIdAsync(id, cancellationToken);
        if (project is null) return NotFound();

        if (project.Status != "open" || project.SlotsAvailable <= 0)
            return BadRequest(new ErrorResponse("Este projeto não está aceitando candidaturas no momento."));

        var alreadyApplied = await _uow.LabApplications.HasAppliedAsync(userId, id, cancellationToken);
        if (alreadyApplied)
            return BadRequest(new ErrorResponse("Você já se candidatou a este projeto."));

        var application = LabApplication.Create(userId, id);
        await _uow.LabApplications.AddAsync(application, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetMyApplications), new LabApplicationDto(
            application.Id, id, project.Title, application.Status.ToString(), application.AppliedAt));
    }

    /// <summary>Retorna todas as candidaturas do usuário autenticado com seus respectivos status.</summary>
    [HttpGet("my-applications")]
    [ProducesResponseType(typeof(List<LabApplicationDto>), StatusCodes.Status200OK)]
    [SwaggerOperation(Summary = "Candidaturas do usuário autenticado")]
    public async Task<ActionResult<List<LabApplicationDto>>> GetMyApplications(CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetUserId();
        var applications = await _uow.LabApplications.GetByUserAsync(userId, cancellationToken);

        var projectIds = applications.Select(a => a.ProjectId).ToHashSet();
        var projects = await _uow.LabProjects.FindAsync(p => projectIds.Contains(p.Id), cancellationToken);
        var projectById = projects.ToDictionary(p => p.Id);

        var dtos = applications.Select(a =>
        {
            projectById.TryGetValue(a.ProjectId, out var project);
            return new LabApplicationDto(
                a.Id, a.ProjectId, project?.Title ?? "—", a.Status.ToString(), a.AppliedAt);
        }).ToList();

        return Ok(dtos);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<ObjectResult?> RequireMinLevelAsync(
        Guid userId, UserLevel minLevel, CancellationToken ct)
    {
        var user = await _uow.Users.GetByIdAsync(userId, ct);
        if (user is null || user.Level < minLevel)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new ErrorResponse($"Acesso restrito a usuários de nível {minLevel} ou superior."));
        }

        return null;
    }
}

public record LabProjectDto(Guid Id, string Title, string Description, string Stack, string Status, int SlotsAvailable, DateTime CreatedAt);
public record LabApplicationDto(Guid Id, Guid ProjectId, string ProjectTitle, string Status, DateTime AppliedAt);
