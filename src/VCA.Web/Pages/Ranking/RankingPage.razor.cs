using System.Globalization;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using VCA.Web.Components.Ranking;
using VCA.Web.Services;

namespace VCA.Web.Pages.Ranking;

/// <summary>
/// Code-behind da página de ranking com tabs Semanal / Mensal / Hall of Fame.
/// Atualização em tempo real via SignalR (RankingHubClient).
/// </summary>
public partial class RankingPage : ComponentBase, IAsyncDisposable
{
    [Inject] private UserHttpService UserService { get; set; } = default!;
    [Inject] private RankingService RankingService { get; set; } = default!;
    [Inject] private IConfiguration Configuration { get; set; } = default!;
    [Inject] private AuthService AuthService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    private MudTabs? _tabs;
    private int _activeTab;

    // Dados de ranking
    private List<RankingRowDto> _weeklyEntries = [];
    private List<RankingRowDto> _monthlyEntries = [];
    private List<RankingRowDto> _hallOfFameEntries = [];

    private bool _loadingWeekly = true;
    private bool _loadingMonthly = true;
    private bool _loadingHallOfFame = true;

    // Posição do usuário logado
    private MyRankingPositionDto? _myPosition;
    private Guid? _myUserId;

    // SignalR
    private RankingHubClient? _hubClient;

    protected override async Task OnInitializedAsync()
    {
        // Descobre userId do token
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var sub = authState.User.FindFirst("sub")?.Value;
        if (Guid.TryParse(sub, out var uid)) _myUserId = uid;

        // Carrega todas as tabs em paralelo
        var weeklyTask = LoadWeeklyAsync();
        var monthlyTask = LoadMonthlyAsync();
        var hallTask = LoadHallOfFameAsync();
        var posTask = LoadMyPositionAsync();

        await Task.WhenAll(weeklyTask, monthlyTask, hallTask, posTask);

        // Liga SignalR
        var apiBase = Configuration["ApiBaseUrl"] ?? "http://localhost:5000";
        _hubClient = new RankingHubClient(apiBase);
        _hubClient.OnRankingUpdated += HandleRankingUpdate;

        try { await _hubClient.StartAsync(accessToken: AuthService.Token); }
        catch { /* sem bloqueio se SignalR falhar */ }
    }

    // ── Carregadores ──────────────────────────────────────────────────────────

    private async Task LoadWeeklyAsync()
    {
        _loadingWeekly = true;
        try
        {
            var data = await RankingService.GetWeeklyRankingAsync();
            _weeklyEntries = data?
                .Select(r => new RankingRowDto(r.Position, r.UserId, r.UserName, r.AvatarUrl, r.XpEarned, ""))
                .ToList() ?? [];
        }
        catch { _weeklyEntries = []; }
        finally { _loadingWeekly = false; }
    }

    private async Task LoadMonthlyAsync()
    {
        _loadingMonthly = true;
        try
        {
            // Usa o endpoint mensal via HttpClient direto (sem wrapper no RankingService existente)
            var http = RankingService.GetHttp();
            var dto = await http.GetFromJsonAsync<MonthlyRankingResponse>("api/ranking/monthly");
            _monthlyEntries = dto?.Entries
                .Select(e => new RankingRowDto(e.Position, Guid.Empty, e.UserName, e.AvatarUrl, e.TotalXp, ""))
                .ToList() ?? [];
        }
        catch { _monthlyEntries = []; }
        finally { _loadingMonthly = false; }
    }

    private async Task LoadHallOfFameAsync()
    {
        _loadingHallOfFame = true;
        try
        {
            var http = RankingService.GetHttp();
            var data = await http.GetFromJsonAsync<List<HallOfFameEntry>>("api/ranking/hall-of-fame");
            _hallOfFameEntries = data?
                .Select(e => new RankingRowDto(e.Position, e.UserId, e.UserName, e.AvatarUrl, e.TotalXp, e.Level))
                .ToList() ?? [];
        }
        catch { _hallOfFameEntries = []; }
        finally { _loadingHallOfFame = false; }
    }

    private async Task LoadMyPositionAsync()
    {
        try { _myPosition = await UserService.GetMyRankingPositionAsync(); }
        catch { _myPosition = null; }
    }

    // ── SignalR ───────────────────────────────────────────────────────────────

    private async Task HandleRankingUpdate(RankingUpdateMessage msg)
    {
        await InvokeAsync(() =>
        {
            // Atualiza a lista semanal in-place para animar a transição via CSS
            var existing = _weeklyEntries.FirstOrDefault(e => e.UserId == msg.UserId);
            if (existing is not null)
            {
                var idx = _weeklyEntries.IndexOf(existing);
                _weeklyEntries[idx] = existing with { Position = msg.Position, XpEarned = msg.XpEarned };
            }
            else
            {
                _weeklyEntries.Add(new RankingRowDto(
                    msg.Position, msg.UserId, msg.UserName, msg.AvatarUrl, msg.XpEarned, msg.Level));
            }

            // Re-ordena pela nova posição
            _weeklyEntries = [.. _weeklyEntries.OrderBy(e => e.Position)];

            // Atualiza a posição do próprio usuário
            if (msg.UserId == _myUserId && _myPosition is not null)
                _myPosition = _myPosition with { Position = msg.Position, XpEarnedThisWeek = msg.XpEarned };

            StateHasChanged();
        });
    }

    internal void OnTabChanged(int index) => _activeTab = index;

    // ── Helpers ───────────────────────────────────────────────────────────────

    internal static string PositionLabel(int pos) => pos switch
    {
        0 => "—",
        1 => "🥇 1º",
        2 => "🥈 2º",
        3 => "🥉 3º",
        _ => $"{pos}º"
    };

    public async ValueTask DisposeAsync()
    {
        if (_hubClient is not null)
        {
            _hubClient.OnRankingUpdated -= HandleRankingUpdate;
            await _hubClient.DisposeAsync();
        }
    }
}

// ── DTOs internos ─────────────────────────────────────────────────────────────

public record RankingRowDto(int Position, Guid UserId, string UserName, string? AvatarUrl, int XpEarned, string Level);

file record MonthlyRankingResponse(string Month, List<MonthlyEntry> Entries);
file record MonthlyEntry(int Position, string UserName, string? AvatarUrl, int TotalXp);
file record HallOfFameEntry(int Position, Guid UserId, string UserName, string? AvatarUrl, int TotalXp, string Level);
