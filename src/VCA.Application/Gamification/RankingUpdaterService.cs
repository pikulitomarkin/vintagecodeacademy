using System.Globalization;
using Microsoft.Extensions.Logging;
using VCA.Application.Interfaces;
using VCA.Domain.Entities;
using VCA.Domain.Interfaces;

namespace VCA.Application.Gamification;

/// <summary>
/// Serviço responsável por manter o ranking semanal atualizado.
/// Faz upsert da entrada do usuário e recalcula as posições dos top 100.
/// Após recalcular, transmite a nova posição do usuário via IRankingBroadcaster (SignalR).
/// </summary>
public class RankingUpdaterService
{
    private readonly IUnitOfWork _uow;
    private readonly IRankingBroadcaster _broadcaster;
    private readonly ILogger<RankingUpdaterService> _logger;

    public RankingUpdaterService(
        IUnitOfWork uow,
        IRankingBroadcaster broadcaster,
        ILogger<RankingUpdaterService> logger)
    {
        _uow = uow;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    /// <summary>
    /// Faz upsert do XP do usuário na semana atual, recalcula posições dos top 100
    /// e transmite a nova posição via SignalR.
    /// </summary>
    public async Task UpsertAsync(Guid userId, int xpEarned, CancellationToken cancellationToken = default)
    {
        var currentWeek = GetCurrentWeekNumber();

        var existing = await _uow.Rankings.GetByUserAndWeekAsync(userId, currentWeek, cancellationToken);

        if (existing is not null)
        {
            existing.UpdateXp(existing.XpEarned + xpEarned);
        }
        else
        {
            var entry = Ranking.Create(userId, currentWeek, xpEarned, position: 0);
            await _uow.Rankings.AddAsync(entry, cancellationToken);
        }

        await _uow.SaveChangesAsync(cancellationToken);
        await RecalculatePositionsAsync(currentWeek, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        // Transmite a posição atualizada do usuário em tempo real
        var updated = await _uow.Rankings.GetByUserAndWeekAsync(userId, currentWeek, cancellationToken);
        if (updated is not null)
        {
            var user = await _uow.Users.GetByIdAsync(userId, cancellationToken);
            if (user is not null)
            {
                await _broadcaster.BroadcastAsync(new RankingBroadcastEntry(
                    Week: currentWeek.ToString(),
                    Position: updated.Position,
                    UserId: userId,
                    UserName: user.Name,
                    AvatarUrl: user.AvatarUrl,
                    XpEarned: updated.XpEarned,
                    Level: user.Level.ToString()), cancellationToken);
            }
        }

        _logger.LogInformation(
            "Ranking atualizado: UserId={UserId} Semana={Week} XPAdicionado={Xp}",
            userId, currentWeek, xpEarned);
    }

    private async Task RecalculatePositionsAsync(int week, CancellationToken cancellationToken)
    {
        var top100 = await _uow.Rankings.GetWeeklyTopAsync(week, 100, cancellationToken);
        var sorted = top100.OrderByDescending(r => r.XpEarned).ToList();

        for (int i = 0; i < sorted.Count; i++)
            sorted[i].UpdatePosition(i + 1);
    }

    private static int GetCurrentWeekNumber()
    {
        var today = DateTime.UtcNow;
        var week = ISOWeek.GetWeekOfYear(today);
        var year = ISOWeek.GetYear(today);
        return year * 100 + week;
    }
}
