using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VCA.Application.Auth.Common;
using VCA.Application.Interfaces;

namespace VCA.Infrastructure.ExternalServices;

/// <summary>
/// Wrapper REST sobre Supabase Auth (gotrue). Centraliza erros de credencial em
/// UnauthorizedAccessException e nunca expõe corpo bruto da API ao chamador.
/// </summary>
public sealed class SupabaseAuthService : ISupabaseAuthService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly ILogger<SupabaseAuthService> _logger;
    private readonly string _baseUrl;
    private readonly string _anonKey;

    public SupabaseAuthService(HttpClient http, IConfiguration config, ILogger<SupabaseAuthService> logger)
    {
        _http = http;
        _logger = logger;
        _baseUrl = (config["Supabase:Url"]
            ?? throw new InvalidOperationException("Supabase:Url não configurado.")).TrimEnd('/');
        _anonKey = config["Supabase:AnonKey"]
            ?? throw new InvalidOperationException("Supabase:AnonKey não configurado.");

        if (_http.BaseAddress is null)
            _http.BaseAddress = new Uri(_baseUrl + "/auth/v1/");
        if (!_http.DefaultRequestHeaders.Contains("apikey"))
            _http.DefaultRequestHeaders.Add("apikey", _anonKey);
    }

    public async Task<SupabaseUser> RegisterAsync(string email, string password, string name, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            email,
            password,
            data = new { name }
        };

        using var resp = await _http.PostAsJsonAsync("signup", payload, cancellationToken);
        var body = await resp.Content.ReadAsStringAsync(cancellationToken);

        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("Supabase signup falhou: status={Status} body={Body}", (int)resp.StatusCode, Truncate(body));
            if (resp.StatusCode is HttpStatusCode.UnprocessableEntity or HttpStatusCode.Conflict)
                throw new InvalidOperationException("E-mail already registered or invalid input.");
            throw new InvalidOperationException("Falha ao registrar no Supabase Auth.");
        }

        return ParseUser(body);
    }

    public async Task<SupabaseSession> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var payload = new { email, password };

        using var resp = await _http.PostAsJsonAsync("token?grant_type=password", payload, cancellationToken);
        var body = await resp.Content.ReadAsStringAsync(cancellationToken);

        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("Supabase login falhou: status={Status}", (int)resp.StatusCode);
            // 400/401 → credencial inválida; demais → erro genérico — sempre traduzimos para Unauthorized
            // para evitar enumeração (não vazamos se o e-mail existe).
            throw new UnauthorizedAccessException("Credenciais inválidas.");
        }

        return ParseSession(body);
    }

    public string LoginWithGoogleUrl(string redirectTo) => BuildOAuthUrl("google", redirectTo);
    public string LoginWithGitHubUrl(string redirectTo) => BuildOAuthUrl("github", redirectTo);

    public async Task<SupabaseSession> RefreshSessionAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var payload = new { refresh_token = refreshToken };

        using var resp = await _http.PostAsJsonAsync("token?grant_type=refresh_token", payload, cancellationToken);
        var body = await resp.Content.ReadAsStringAsync(cancellationToken);

        if (!resp.IsSuccessStatusCode)
            throw new UnauthorizedAccessException("Refresh token inválido ou expirado.");

        return ParseSession(body);
    }

    public async Task SignOutAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "logout");
        req.Headers.Add("Authorization", $"Bearer {accessToken}");
        try
        {
            using var resp = await _http.SendAsync(req, cancellationToken);
            if (!resp.IsSuccessStatusCode)
                _logger.LogInformation("Supabase logout retornou status {Status} (best-effort).", (int)resp.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao chamar Supabase logout (best-effort).");
        }
    }

    public async Task<SupabaseUser?> GetUserFromTokenAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "user");
        req.Headers.Add("Authorization", $"Bearer {accessToken}");

        using var resp = await _http.SendAsync(req, cancellationToken);
        if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            return null;
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("GET /auth/v1/user retornou status {Status}.", (int)resp.StatusCode);
            return null;
        }

        var body = await resp.Content.ReadAsStringAsync(cancellationToken);
        return ParseUser(body);
    }

    private string BuildOAuthUrl(string provider, string redirectTo)
    {
        if (string.IsNullOrWhiteSpace(redirectTo))
            throw new ArgumentException("redirectTo é obrigatório.", nameof(redirectTo));
        // Defesa contra open-redirect: o caller (controller) deve garantir que redirectTo está em allowlist.
        var encoded = Uri.EscapeDataString(redirectTo);
        return $"{_baseUrl}/auth/v1/authorize?provider={provider}&redirect_to={encoded}";
    }

    private static SupabaseSession ParseSession(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var accessToken = root.GetProperty("access_token").GetString() ?? throw new InvalidOperationException("access_token ausente.");
        var refreshToken = root.GetProperty("refresh_token").GetString() ?? throw new InvalidOperationException("refresh_token ausente.");
        var tokenType = root.TryGetProperty("token_type", out var tt) ? tt.GetString() ?? "bearer" : "bearer";
        var expiresIn = root.TryGetProperty("expires_in", out var ei) ? ei.GetInt32() : 3600;
        var expiresAt = DateTime.UtcNow.AddSeconds(expiresIn);

        SupabaseUser user;
        if (root.TryGetProperty("user", out var userEl))
            user = ParseUserElement(userEl);
        else
            throw new InvalidOperationException("Resposta sem objeto 'user'.");

        return new SupabaseSession(accessToken, refreshToken, tokenType, expiresIn, expiresAt, user);
    }

    private static SupabaseUser ParseUser(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return ParseUserElement(doc.RootElement);
    }

    private static SupabaseUser ParseUserElement(JsonElement el)
    {
        var idStr = el.GetProperty("id").GetString() ?? throw new InvalidOperationException("user.id ausente.");
        var id = Guid.Parse(idStr);
        var email = el.TryGetProperty("email", out var em) ? em.GetString() ?? string.Empty : string.Empty;
        var emailConfirmed = el.TryGetProperty("email_confirmed_at", out var ec) && ec.ValueKind != JsonValueKind.Null;

        string? name = null, avatarUrl = null, provider = null;
        if (el.TryGetProperty("user_metadata", out var meta) && meta.ValueKind == JsonValueKind.Object)
        {
            if (meta.TryGetProperty("name", out var n)) name = n.GetString();
            if (meta.TryGetProperty("full_name", out var fn) && string.IsNullOrEmpty(name)) name = fn.GetString();
            if (meta.TryGetProperty("avatar_url", out var av)) avatarUrl = av.GetString();
        }
        if (el.TryGetProperty("app_metadata", out var app) && app.ValueKind == JsonValueKind.Object
            && app.TryGetProperty("provider", out var pv))
            provider = pv.GetString();

        return new SupabaseUser(id, email, name, avatarUrl, provider, emailConfirmed);
    }

    private static string Truncate(string s, int max = 500) =>
        string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max] + "…";
}
