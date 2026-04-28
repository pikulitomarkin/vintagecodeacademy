using Microsoft.AspNetCore.SignalR;

namespace VCA.API.Hubs;

/// <summary>
/// Hub SignalR para transmissão em tempo real do ranking semanal.
/// Clientes se inscrevem no grupo "ranking-{week}" e recebem atualizações
/// individuais de entrada sem necessidade de recarregar a lista inteira.
/// </summary>
public class RankingHub : Hub
{
    /// <summary>
    /// Inscreve o cliente no grupo de ranking da semana especificada.
    /// Se week for nulo, usa a semana atual no formato YYYYWW.
    /// </summary>
    public async Task JoinWeekGroup(string? week)
    {
        var groupName = RankingGroupName(week ?? CurrentWeekKey());
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    /// <summary>Remove o cliente do grupo de ranking da semana especificada.</summary>
    public async Task LeaveWeekGroup(string? week)
    {
        var groupName = RankingGroupName(week ?? CurrentWeekKey());
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Transmite uma atualização de posição para todos no grupo da semana.
    /// Chamado internamente pelo RankingUpdaterService após XP concedido.
    /// </summary>
    public async Task BroadcastRankingUpdate(RankingUpdatePayload payload)
    {
        var groupName = RankingGroupName(payload.Week);
        await Clients.Group(groupName).SendAsync("RankingUpdated", payload);
    }

    public override async Task OnConnectedAsync()
    {
        // Auto-inscreve na semana atual ao conectar
        await JoinWeekGroup(null);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }

    internal static string RankingGroupName(string week) => $"ranking-{week}";

    internal static string CurrentWeekKey()
    {
        var today = DateTime.UtcNow;
        var week = System.Globalization.ISOWeek.GetWeekOfYear(today);
        var year = System.Globalization.ISOWeek.GetYear(today);
        return $"{year * 100 + week}";
    }
}

/// <summary>Payload enviado ao cliente SignalR com a entrada de ranking atualizada.</summary>
public record RankingUpdatePayload(
    string Week,
    int Position,
    Guid UserId,
    string UserName,
    string? AvatarUrl,
    int XpEarned,
    string Level);
