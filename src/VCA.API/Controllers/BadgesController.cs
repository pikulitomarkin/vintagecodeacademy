using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using VCA.API.Extensions;
using VCA.Domain.Interfaces;

namespace VCA.API.Controllers;

/// <summary>
/// Endpoints de badges — lista todas as conquistas com status earned/not-earned do usuário.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BadgesController : ControllerBase
{
    private readonly IUnitOfWork _uow;

    public BadgesController(IUnitOfWork uow) => _uow = uow;

    /// <summary>
    /// Retorna todos os badges do sistema com indicação de quais o usuário autenticado já conquistou.
    /// Badges não conquistados aparecem com EarnedAt nulo e IsEarned = false.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<BadgeStatusDto>), StatusCodes.Status200OK)]
    [SwaggerOperation(Summary = "Lista badges com status earned/not-earned do usuário")]
    public async Task<ActionResult<List<BadgeStatusDto>>> GetAll(CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetUserId();

        var allBadges = await _uow.Badges.GetAllAsync(cancellationToken);
        var earnedBadges = await _uow.Badges.GetByUserAsync(userId, cancellationToken);

        var earnedCodes = earnedBadges
            .Select(b => b.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Carrega as datas de conquista dos user_badges do usuário
        var userBadgeDates = (await _uow.Users.GetByIdAsync(userId, cancellationToken))
            ?.Badges
            .ToDictionary(ub => ub.BadgeId, ub => ub.EarnedAt)
            ?? [];

        var dtos = allBadges
            .OrderBy(b => b.Code)
            .Select(b =>
            {
                var isEarned = earnedCodes.Contains(b.Code);
                userBadgeDates.TryGetValue(b.Id, out var earnedAt);
                return new BadgeStatusDto(
                    b.Code, b.Name, b.Description, b.IconUrl, b.XpBonus,
                    isEarned, isEarned ? earnedAt : null);
            })
            .ToList();

        return Ok(dtos);
    }
}

public record BadgeStatusDto(
    string Code,
    string Name,
    string Description,
    string? IconUrl,
    int XpBonus,
    bool IsEarned,
    DateTime? EarnedAt);
