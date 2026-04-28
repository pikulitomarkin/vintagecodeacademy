using MediatR;
using Microsoft.Extensions.Logging;
using VCA.Application.Gamification.Events;
using VCA.Domain.Common;
using VCA.Domain.Interfaces;

namespace VCA.Application.Gamification.Commands;

/// <summary>
/// Handler MediatR que concede XP, verifica subida de nível e publica XpAwardedEvent.
/// </summary>
public class AwardXpCommandHandler : IRequestHandler<AwardXpCommand, Result<XpEventDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly IPublisher _publisher;
    private readonly RankingUpdaterService _rankingUpdater;
    private readonly ILogger<AwardXpCommandHandler> _logger;

    public AwardXpCommandHandler(
        IUnitOfWork uow,
        IPublisher publisher,
        RankingUpdaterService rankingUpdater,
        ILogger<AwardXpCommandHandler> logger)
    {
        _uow = uow;
        _publisher = publisher;
        _rankingUpdater = rankingUpdater;
        _logger = logger;
    }

    public async Task<Result<XpEventDto>> Handle(AwardXpCommand request, CancellationToken cancellationToken)
    {
        var user = await _uow.Users.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
            return Result.Failure<XpEventDto>("Usuário não encontrado.");

        var xpAmount = XpEvents.ForReason(request.Reason);
        if (xpAmount <= 0)
            return Result.Failure<XpEventDto>($"Razão de XP inválida: {request.Reason}.");

        var previousLevel = user.Level;
        var previousLastActivityAt = user.LastActivityAt;

        user.AddXp(xpAmount);

        var levelChanged = user.Level != previousLevel;
        var newLevel = levelChanged ? user.Level.ToString() : null;

        await _rankingUpdater.UpsertAsync(request.UserId, xpAmount, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "XP concedido: UserId={UserId} Razão={Reason} XP={Xp} NívelAntes={Prev} NívelDepois={New}",
            request.UserId, request.Reason, xpAmount, previousLevel, user.Level);

        var notification = new XpAwardedEvent(
            request.UserId,
            xpAmount,
            user.Xp,
            previousLevel.ToString(),
            newLevel,
            request.Reason,
            request.EntityId,
            previousLastActivityAt);

        await _publisher.Publish(notification, cancellationToken);

        return Result.Success(new XpEventDto(
            request.UserId,
            xpAmount,
            user.Xp,
            previousLevel.ToString(),
            newLevel));
    }
}
