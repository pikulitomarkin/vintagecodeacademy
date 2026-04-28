using MediatR;
using VCA.Domain.Common;

namespace VCA.Application.Gamification.Commands;

/// <summary>
/// Comando MediatR para verificar e atualizar o streak diário do usuário.
/// PreviousLastActivityAt deve ser passado quando chamado a partir do fluxo DailyLogin
/// (capturado antes de user.AddXp() alterar LastActivityAt).
/// </summary>
public record UpdateStreakCommand(Guid UserId, DateTime? PreviousLastActivityAt = null)
    : IRequest<Result<StreakUpdateDto>>;

/// <summary>
/// DTO com o resultado da atualização de streak.
/// </summary>
public record StreakUpdateDto(int StreakDays, bool Incremented, int BonusXpAwarded);
