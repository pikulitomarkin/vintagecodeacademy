using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using VCA.API.Extensions;
using VCA.Application.Gamification;
using VCA.Application.Gamification.Commands;
using VCA.Domain.Interfaces;

namespace VCA.API.Controllers;

/// <summary>
/// Endpoints de perfil de usuário.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUnitOfWork _uow;
    private readonly ISender _sender;

    public UsersController(IUnitOfWork uow, ISender sender)
    {
        _uow = uow;
        _sender = sender;
    }

    /// <summary>Retorna o perfil completo do usuário autenticado.</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [SwaggerOperation(Summary = "Perfil completo do usuário autenticado")]
    public async Task<ActionResult<UserProfileDto>> GetMe(CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetUserId();
        var user = await _uow.Users.GetByIdAsync(userId, cancellationToken);

        if (user is null) return NotFound();

        var earnedBadges = await _uow.Badges.GetByUserAsync(userId, cancellationToken);

        return Ok(new UserProfileDto(
            user.Id,
            user.Email,
            user.Name,
            user.AvatarUrl,
            user.Xp,
            user.Level.ToString(),
            user.StreakDays,
            user.CreatedAt,
            earnedBadges.Select(b => new BadgeSummaryDto(b.Code, b.Name, b.IconUrl)).ToList()));
    }

    /// <summary>Atualiza nome e avatar do usuário autenticado.</summary>
    [HttpPut("me")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [SwaggerOperation(Summary = "Atualiza perfil do usuário autenticado")]
    public async Task<IActionResult> UpdateMe(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetUserId();
        var user = await _uow.Users.GetByIdAsync(userId, cancellationToken);

        if (user is null) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new ErrorResponse("O nome não pode ser vazio."));

        user.UpdateProfile(request.Name, request.AvatarUrl);
        await _uow.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    /// <summary>Retorna o perfil público de qualquer usuário (sem dados sensíveis).</summary>
    [HttpGet("{id:guid}/profile")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PublicProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [SwaggerOperation(Summary = "Perfil público de um usuário")]
    public async Task<ActionResult<PublicProfileDto>> GetPublicProfile(Guid id, CancellationToken cancellationToken)
    {
        var user = await _uow.Users.GetByIdAsync(id, cancellationToken);
        if (user is null) return NotFound();

        var earnedBadges = await _uow.Badges.GetByUserAsync(id, cancellationToken);

        return Ok(new PublicProfileDto(
            user.Id,
            user.Name,
            user.AvatarUrl,
            user.Xp,
            user.Level.ToString(),
            user.StreakDays,
            earnedBadges.Select(b => new BadgeSummaryDto(b.Code, b.Name, b.IconUrl)).ToList()));
    }

    /// <summary>
    /// Registra o login diário, atualiza streak e concede XP de DailyLogin.
    /// Idempotente — repetições no mesmo dia não concederão XP duplicado via UpdateStreakCommand.
    /// </summary>
    [HttpPost("me/daily-login")]
    [ProducesResponseType(typeof(DailyLoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [SwaggerOperation(Summary = "Registra login diário e concede XP")]
    public async Task<ActionResult<DailyLoginResponse>> DailyLogin(CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetUserId();

        var xpResult = await _sender.Send(
            new AwardXpCommand(userId, XpReason.DailyLogin),
            cancellationToken);

        if (xpResult.IsFailure)
            return BadRequest(new ErrorResponse(xpResult.Error!));

        return Ok(new DailyLoginResponse(
            xpResult.Value!.XpAwarded,
            xpResult.Value.NewTotalXp,
            xpResult.Value.PreviousLevel,
            xpResult.Value.NewLevel));
    }
}

public record UpdateProfileRequest(string Name, string? AvatarUrl);
public record UserProfileDto(
    Guid Id, string Email, string Name, string? AvatarUrl,
    int Xp, string Level, int StreakDays, DateTime CreatedAt,
    List<BadgeSummaryDto> Badges);
public record PublicProfileDto(
    Guid Id, string Name, string? AvatarUrl,
    int Xp, string Level, int StreakDays,
    List<BadgeSummaryDto> Badges);
public record BadgeSummaryDto(string Code, string Name, string? IconUrl);
public record DailyLoginResponse(int XpAwarded, int NewTotalXp, string PreviousLevel, string? NewLevel);
public record ErrorResponse(string Error);
