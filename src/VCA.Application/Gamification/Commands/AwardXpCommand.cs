using MediatR;
using VCA.Domain.Common;

namespace VCA.Application.Gamification.Commands;

/// <summary>
/// Comando MediatR para conceder XP a um usuário por um evento específico.
/// </summary>
public record AwardXpCommand(Guid UserId, XpReason Reason, Guid? EntityId = null)
    : IRequest<Result<XpEventDto>>;

/// <summary>
/// DTO retornado após a concessão de XP ao usuário.
/// </summary>
public record XpEventDto(
    Guid UserId,
    int XpAwarded,
    int NewTotalXp,
    string PreviousLevel,
    string? NewLevel);
