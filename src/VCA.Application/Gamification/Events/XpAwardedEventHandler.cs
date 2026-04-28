using MediatR;
using Microsoft.Extensions.Logging;
using VCA.Application.Gamification.Commands;

namespace VCA.Application.Gamification.Events;

/// <summary>
/// Handler MediatR para XpAwardedEvent.
/// Ordem: (1) atualiza streak se DailyLogin, (2) verifica e concede badges.
/// A ordem garante que OnFire seja avaliado com o streak já atualizado.
/// </summary>
public class XpAwardedEventHandler : INotificationHandler<XpAwardedEvent>
{
    private readonly ISender _sender;
    private readonly BadgeCheckerService _badgeChecker;
    private readonly ILogger<XpAwardedEventHandler> _logger;

    public XpAwardedEventHandler(
        ISender sender,
        BadgeCheckerService badgeChecker,
        ILogger<XpAwardedEventHandler> logger)
    {
        _sender = sender;
        _badgeChecker = badgeChecker;
        _logger = logger;
    }

    public async Task Handle(XpAwardedEvent notification, CancellationToken cancellationToken)
    {
        // Atualiza streak antes da verificação de badges para garantir que
        // OnFire (streak >= 30) seja avaliado com o valor já atualizado.
        if (notification.Reason == XpReason.DailyLogin)
        {
            var streakCommand = new UpdateStreakCommand(
                notification.UserId,
                notification.PreviousLastActivityAt);

            var streakResult = await _sender.Send(streakCommand, cancellationToken);

            if (streakResult.IsFailure)
            {
                _logger.LogWarning(
                    "Falha ao atualizar streak para UserId={UserId}: {Erro}",
                    notification.UserId, streakResult.Error);
            }
        }

        // Verifica e concede todos os badges possíveis para o usuário
        await _badgeChecker.CheckAndGrantBadgesAsync(notification.UserId, cancellationToken);
    }
}
