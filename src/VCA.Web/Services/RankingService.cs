using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;
using VCA.Web.Pages;

namespace VCA.Web.Services;

/// <summary>
/// Serviço Blazor para ranking semanal com atualização em tempo real via SignalR.
/// </summary>
public class RankingService : IAsyncDisposable
{
    private readonly HttpClient _http;
    private HubConnection? _hubConnection;
    private Action<List<Ranking.RankingDto>>? _onUpdate;

    public RankingService(HttpClient http) => _http = http;

    public async Task<List<Ranking.RankingDto>?> GetWeeklyRankingAsync()
        => await _http.GetFromJsonAsync<List<Ranking.RankingDto>>("api/ranking/weekly");

    public async Task SubscribeToUpdatesAsync(Action<List<Ranking.RankingDto>> onUpdate)
    {
        _onUpdate = onUpdate;

        var hubUrl = new Uri(new Uri(_http.BaseAddress!.ToString()), "/hubs/ranking");
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<List<Ranking.RankingDto>>("RankingUpdated", ranking => _onUpdate?.Invoke(ranking));
        await _hubConnection.StartAsync();
    }

    public async Task UnsubscribeAsync()
    {
        if (_hubConnection is not null)
            await _hubConnection.StopAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
            await _hubConnection.DisposeAsync();
    }
}
