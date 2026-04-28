using Microsoft.AspNetCore.SignalR;
using VCA.Application.Interfaces;
using VCA.API.Hubs;

namespace VCA.API.Services;

/// <summary>
/// Implementação de IRankingBroadcaster que usa o SignalR RankingHub
/// para transmitir atualizações de ranking em tempo real.
/// </summary>
public class SignalRRankingBroadcaster : IRankingBroadcaster
{
    private readonly IHubContext<RankingHub> _hubContext;

    public SignalRRankingBroadcaster(IHubContext<RankingHub> hubContext)
        => _hubContext = hubContext;

    public async Task BroadcastAsync(RankingBroadcastEntry entry, CancellationToken cancellationToken = default)
    {
        var groupName = RankingHub.RankingGroupName(entry.Week);

        var payload = new RankingUpdatePayload(
            entry.Week,
            entry.Position,
            entry.UserId,
            entry.UserName,
            entry.AvatarUrl,
            entry.XpEarned,
            entry.Level);

        await _hubContext.Clients
            .Group(groupName)
            .SendAsync("RankingUpdated", payload, cancellationToken);
    }
}
