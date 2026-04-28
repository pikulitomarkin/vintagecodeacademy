using Microsoft.AspNetCore.SignalR.Client;

namespace VCA.Web.Components.Ranking;

/// <summary>
/// Cliente SignalR para o RankingHub da VCA API.
/// Conecta ao grupo da semana atual e notifica o componente quando há atualização de ranking.
/// Implementa reconexão automática com exponential backoff.
/// </summary>
public sealed class RankingHubClient : IAsyncDisposable
{
    private HubConnection? _connection;
    private readonly string _hubUrl;
    private bool _started;

    public event Func<RankingUpdateMessage, Task>? OnRankingUpdated;

    public HubConnectionState State => _connection?.State ?? HubConnectionState.Disconnected;

    public RankingHubClient(string apiBaseUrl)
    {
        // Normaliza a URL base e apende o caminho do hub
        var baseUri = new Uri(apiBaseUrl.TrimEnd('/'));
        _hubUrl = new Uri(baseUri, "/hubs/ranking").ToString();
    }

    /// <summary>
    /// Conecta ao hub e inscreve no grupo da semana especificada (formato YYYYWW).
    /// Se week for nulo, o servidor usa a semana atual automaticamente.
    /// </summary>
    public async Task StartAsync(string? week = null, string? accessToken = null)
    {
        if (_started) return;

        _connection = new HubConnectionBuilder()
            .WithUrl(_hubUrl, options =>
            {
                if (!string.IsNullOrWhiteSpace(accessToken))
                    options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
            })
            .WithAutomaticReconnect(new ExponentialBackoffRetryPolicy())
            .Build();

        _connection.On<RankingUpdateMessage>("RankingUpdated", async msg =>
        {
            if (OnRankingUpdated is not null)
                await OnRankingUpdated.Invoke(msg);
        });

        _connection.Reconnected += async _ =>
        {
            // Re-inscreve no grupo após reconexão
            if (_connection.State == HubConnectionState.Connected)
                await _connection.InvokeAsync("JoinWeekGroup", week);
        };

        await _connection.StartAsync();

        // Inscreve-se explicitamente no grupo da semana
        await _connection.InvokeAsync("JoinWeekGroup", week);

        _started = true;
    }

    /// <summary>Desconecta do hub graciosamente.</summary>
    public async Task StopAsync()
    {
        if (_connection is not null && _started)
        {
            await _connection.StopAsync();
            _started = false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}

/// <summary>Mensagem recebida via SignalR quando uma posição de ranking muda.</summary>
public record RankingUpdateMessage(
    string Week,
    int Position,
    Guid UserId,
    string UserName,
    string? AvatarUrl,
    int XpEarned,
    string Level);

/// <summary>
/// Política de reconexão com exponential backoff:
/// 0s, 2s, 5s, 10s, 20s, 30s, 60s (depois mantém 60s).
/// </summary>
file sealed class ExponentialBackoffRetryPolicy : IRetryPolicy
{
    private static readonly TimeSpan[] _delays =
    [
        TimeSpan.Zero,
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(20),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(60)
    ];

    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
        var idx = (int)Math.Min(retryContext.PreviousRetryCount, _delays.Length - 1);
        return _delays[idx];
    }
}
