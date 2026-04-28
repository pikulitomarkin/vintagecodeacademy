using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace VCA.Infrastructure.Auth;

/// <summary>
/// Middleware que valida o JWT Supabase usando JWKS (chaves públicas RS256) com cache.
/// Para projetos Supabase com JWT secret simétrico (HS256), o validador padrão JwtBearer
/// é mantido como fallback no Program.cs — este middleware é a defesa primária quando
/// o projeto usa o emissor com keys públicas.
///
/// Segurança:
///   - Valida assinatura, lifetime, issuer e audience (configuráveis).
///   - Cache JWKS com TTL de 1h e refresh em background; invalida em validação falha por kid desconhecido.
///   - Nunca confia em claims sem validação prévia da assinatura.
/// </summary>
public sealed class JwtValidationMiddleware
{
    public const string JwksCacheKey = "vca:jwks";
    public static readonly TimeSpan JwksCacheTtl = TimeSpan.FromHours(1);

    private readonly RequestDelegate _next;
    private readonly ILogger<JwtValidationMiddleware> _logger;
    private readonly IMemoryCache _cache;
    private readonly string _supabaseUrl;
    private readonly string? _validIssuer;
    private readonly string? _validAudience;
    private readonly bool _enabled;

    public JwtValidationMiddleware(
        RequestDelegate next,
        ILogger<JwtValidationMiddleware> logger,
        IMemoryCache cache,
        IConfiguration config)
    {
        _next = next;
        _logger = logger;
        _cache = cache;
        _supabaseUrl = (config["Supabase:Url"] ?? string.Empty).TrimEnd('/');
        _validIssuer = config["Supabase:Issuer"];
        _validAudience = config["Supabase:Audience"] ?? "authenticated";
        _enabled = bool.TryParse(config["Supabase:UseJwks"], out var v) && v;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Quando JWKS não está habilitado, deixamos o pipeline padrão JwtBearer cuidar.
        if (!_enabled || context.User?.Identity?.IsAuthenticated == true)
        {
            await _next(context);
            return;
        }

        var header = context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var token = header["Bearer ".Length..].Trim();

        try
        {
            var keys = await GetSigningKeysAsync(forceRefresh: false, context.RequestAborted);

            var principal = ValidateToken(token, keys);
            context.User = principal;
        }
        catch (SecurityTokenSignatureKeyNotFoundException)
        {
            // Provável rotação de chaves. Forçamos refresh do JWKS e tentamos novamente uma vez.
            try
            {
                var keys = await GetSigningKeysAsync(forceRefresh: true, context.RequestAborted);
                context.User = ValidateToken(token, keys);
            }
            catch (Exception ex)
            {
                await Reject(context, "Assinatura do token não pôde ser verificada.", ex);
                return;
            }
        }
        catch (SecurityTokenExpiredException)
        {
            await Reject(context, "Token expirado.");
            return;
        }
        catch (SecurityTokenException ex)
        {
            await Reject(context, "Token inválido.", ex);
            return;
        }

        await _next(context);
    }

    private ClaimsPrincipal ValidateToken(string token, IEnumerable<SecurityKey> keys)
    {
        var handler = new JwtSecurityTokenHandler();
        var parameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = keys,
            ValidateIssuer = !string.IsNullOrWhiteSpace(_validIssuer),
            ValidIssuer = _validIssuer,
            ValidateAudience = !string.IsNullOrWhiteSpace(_validAudience),
            ValidAudience = _validAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        return handler.ValidateToken(token, parameters, out _);
    }

    private async Task<IEnumerable<SecurityKey>> GetSigningKeysAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        if (!forceRefresh && _cache.TryGetValue<IList<SecurityKey>>(JwksCacheKey, out var cached) && cached is not null)
            return cached;

        var jwksUrl = $"{_supabaseUrl}/auth/v1/.well-known/jwks.json";
        var configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            jwksUrl,
            new JwksRetriever(),
            new HttpDocumentRetriever { RequireHttps = true });

        if (forceRefresh) configManager.RequestRefresh();

        var oidc = await configManager.GetConfigurationAsync(cancellationToken);
        var keys = oidc.SigningKeys.ToList();

        _cache.Set(JwksCacheKey, keys, JwksCacheTtl);
        _logger.LogInformation("JWKS atualizado: {Count} chaves carregadas (forceRefresh={Force}).", keys.Count, forceRefresh);
        return keys;
    }

    private async Task Reject(HttpContext context, string message, Exception? ex = null)
    {
        if (ex is not null)
            _logger.LogWarning(ex, "Rejeição JWT: {Message}", message);
        else
            _logger.LogWarning("Rejeição JWT: {Message}", message);

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate = $"Bearer error=\"invalid_token\", error_description=\"{message}\"";
        await context.Response.WriteAsJsonAsync(new { error = "unauthorized", message });
    }

    /// <summary>
    /// Adapter mínimo de IConfigurationRetriever para JWKS — Supabase serve JWKS direto,
    /// sem documento OpenID completo. Construímos o OpenIdConnectConfiguration apenas com SigningKeys.
    /// </summary>
    private sealed class JwksRetriever : IConfigurationRetriever<OpenIdConnectConfiguration>
    {
        public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(string address, IDocumentRetriever retriever, CancellationToken cancel)
        {
            var json = await retriever.GetDocumentAsync(address, cancel);
            var config = new OpenIdConnectConfiguration();
            var jwks = new Microsoft.IdentityModel.Tokens.JsonWebKeySet(json);
            foreach (var key in jwks.GetSigningKeys())
                config.SigningKeys.Add(key);
            return config;
        }
    }
}
