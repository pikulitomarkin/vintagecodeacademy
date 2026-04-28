using System.Net.Http.Json;

namespace VCA.Web.Services;

/// <summary>
/// Serviço HTTP tipado para perfil de usuário e posição no ranking.
/// </summary>
public class UserHttpService
{
    private readonly HttpClient _http;

    public UserHttpService(HttpClient http) => _http = http;

    /// <summary>Retorna o perfil completo do usuário autenticado.</summary>
    public Task<UserProfileDto?> GetMyProfileAsync(CancellationToken ct = default)
        => _http.GetFromJsonAsync<UserProfileDto>("api/users/me", ct);

    /// <summary>Retorna o perfil público de qualquer usuário.</summary>
    public Task<UserProfileDto?> GetPublicProfileAsync(Guid userId, CancellationToken ct = default)
        => _http.GetFromJsonAsync<UserProfileDto>($"api/users/{userId}/profile", ct);

    /// <summary>Atualiza nome e/ou avatar do usuário autenticado.</summary>
    public async Task<bool> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync("api/users/me", request, ct);
        return response.IsSuccessStatusCode;
    }

    /// <summary>Retorna a posição do usuário autenticado no ranking semanal atual.</summary>
    public Task<MyRankingPositionDto?> GetMyRankingPositionAsync(CancellationToken ct = default)
        => _http.GetFromJsonAsync<MyRankingPositionDto>("api/ranking/me", ct);
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record UserProfileDto(
    Guid Id,
    string? Email,
    string Name,
    string? AvatarUrl,
    int Xp,
    string Level,
    int StreakDays,
    DateTime CreatedAt,
    List<BadgeSummaryDto> Badges,
    List<CompletedTrailDto>? CompletedTrails = null,
    List<LabContributionDto>? LabContributions = null);

public record BadgeSummaryDto(
    string Code,
    string Name,
    string? IconUrl,
    DateTime? EarnedAt = null);

public record CompletedTrailDto(Guid TrailId, string Title, string Stack, DateTime CompletedAt);

public record LabContributionDto(Guid ProjectId, string ProjectTitle, string Status, DateTime AppliedAt);

public record UpdateProfileRequest(string Name, string? AvatarUrl);

public record MyRankingPositionDto(
    Guid UserId,
    int Position,
    int XpEarnedThisWeek,
    string Week);

// Mapeamento de nível → XP de início e fim para a XpBar
public static class LevelXpMap
{
    private static readonly Dictionary<string, (int Min, int Max)> _thresholds = new()
    {
        ["Rookie"]     = (0,       500),
        ["Apprentice"] = (500,     1_500),
        ["Builder"]    = (1_500,   4_000),
        ["Craftsman"]  = (4_000,  10_000),
        ["Expert"]     = (10_000, 25_000),
        ["VintageDev"] = (25_000, 25_000),
    };

    public static (int CurrentInLevel, int LevelMax) Resolve(string level, int totalXp)
    {
        if (!_thresholds.TryGetValue(level, out var t))
            return (totalXp, totalXp + 1);

        if (t.Min == t.Max) // nível máximo
            return (t.Min, t.Min);

        return (totalXp - t.Min, t.Max - t.Min);
    }
}
