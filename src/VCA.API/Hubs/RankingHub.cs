using Microsoft.AspNetCore.SignalR;

namespace VCA.API.Hubs;

/// <summary>
/// Hub SignalR para transmissão em tempo real do ranking semanal.
/// Clientes conectados recebem atualizações automáticas quando o ranking muda.
/// </summary>
public class RankingHub : Hub
{
    /// <summary>Envia atualização de ranking para todos os clientes conectados.</summary>
    public async Task SendRankingUpdate(object rankingData)
    {
        await Clients.All.SendAsync("RankingUpdated", rankingData);
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }
}
