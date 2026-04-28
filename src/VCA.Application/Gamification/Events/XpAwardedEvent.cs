using MediatR;

namespace VCA.Application.Gamification.Events;

/// <summary>
/// Notificação MediatR publicada após XP ser concedido a um usuário.
/// PreviousLastActivityAt é capturado antes de AddXp() para permitir cálculo
/// correto de streak no XpAwardedEventHandler.
/// </summary>
public record XpAwardedEvent(
    Guid UserId,
    int XpAwarded,
    int NewTotalXp,
    string PreviousLevel,
    string? NewLevel,
    XpReason Reason,
    Guid? EntityId,
    DateTime? PreviousLastActivityAt = null) : INotification;
