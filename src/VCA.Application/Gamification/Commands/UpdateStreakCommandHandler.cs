using MediatR;
using Microsoft.Extensions.Logging;
using VCA.Domain.Common;
using VCA.Domain.Interfaces;

namespace VCA.Application.Gamification.Commands;

/// <summary>
/// Handler que verifica e atualiza o streak diário do usuário.
/// Regras:
///   - Último acesso ontem  → streak++
///   - Último acesso hoje   → sem alteração
///   - Mais de 1 dia atrás  → streak = 1 (reinicia)
/// Milestones (7, 30, 60, 100 dias) concedem XP bônus diretamente.
/// </summary>
public class UpdateStreakCommandHandler : IRequestHandler<UpdateStreakCommand, Result<StreakUpdateDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly RankingUpdaterService _rankingUpdater;
    private readonly ILogger<UpdateStreakCommandHandler> _logger;

    public UpdateStreakCommandHandler(
        IUnitOfWork uow,
        RankingUpdaterService rankingUpdater,
        ILogger<UpdateStreakCommandHandler> logger)
    {
        _uow = uow;
        _rankingUpdater = rankingUpdater;
        _logger = logger;
    }

    public async Task<Result<StreakUpdateDto>> Handle(
        UpdateStreakCommand request, CancellationToken cancellationToken)
    {
        var user = await _uow.Users.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
            return Result.Failure<StreakUpdateDto>("Usuário não encontrado.");

        var today = DateTime.UtcNow.Date;

        // PreviousLastActivityAt é passado quando chamado a partir do fluxo DailyLogin
        // para preservar o valor antes de AddXp() atualizar LastActivityAt.
        var referenceDate = (request.PreviousLastActivityAt ?? user.LastActivityAt)?.Date;

        bool incremented = false;

        if (referenceDate is null || referenceDate < today.AddDays(-1))
        {
            // Nunca logou ou perdeu mais de 1 dia → reinicia streak em 1
            user.ResetStreak();
            user.IncrementStreak();
        }
        else if (referenceDate == today.AddDays(-1))
        {
            // Último acesso foi ontem → incrementa
            user.IncrementStreak();
            incremented = true;
        }
        // else referenceDate == today → já logou hoje, sem alteração

        int bonusXpAwarded = 0;

        if (incremented)
        {
            bonusXpAwarded = user.StreakDays switch
            {
                100 => 500,
                60  => 300,
                30  => 150,
                7   => XpEvents.WeekStreak,
                _   => 0
            };

            if (bonusXpAwarded > 0)
            {
                // XP de milestone de streak concedido diretamente para evitar recursão
                user.AddXp(bonusXpAwarded);
                await _rankingUpdater.UpsertAsync(request.UserId, bonusXpAwarded, cancellationToken);

                _logger.LogInformation(
                    "Streak milestone: UserId={UserId} Dias={Days} BônusXP={Xp}",
                    request.UserId, user.StreakDays, bonusXpAwarded);
            }
        }

        await _uow.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Streak atualizado: UserId={UserId} Dias={Days} Incrementado={Inc}",
            request.UserId, user.StreakDays, incremented);

        return Result.Success(new StreakUpdateDto(user.StreakDays, incremented, bonusXpAwarded));
    }
}
