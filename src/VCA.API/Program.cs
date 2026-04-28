using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using VCA.API.Hubs;
using VCA.API.Middleware;
using VCA.API.Services;
using VCA.Application;
using VCA.Application.Interfaces;
using VCA.Infrastructure;
using VCA.Infrastructure.Auth;

var builder = WebApplication.CreateBuilder(args);

// ─── Sentry ────────────────────────────────────────────────────────────────
builder.WebHost.UseSentry(options =>
{
    options.Dsn = builder.Configuration["Sentry:Dsn"];
    options.TracesSampleRate = 1.0;
    options.Environment = builder.Environment.EnvironmentName;
});

// ─── MVC + Swagger ─────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.EnableAnnotations();
    options.SwaggerDoc("v1", new() { Title = "VintageCodeAcademy API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new()
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Informe o token JWT no formato: Bearer {token}"
    });
    options.AddSecurityRequirement(new()
    {
        {
            new() { Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" } },
            []
        }
    });
});

// ─── Memory cache (RequireLevel + JWKS) ────────────────────────────────────
builder.Services.AddMemoryCache();

// ─── Autenticação JWT (Supabase HS256 fallback) ────────────────────────────
// Quando Supabase:UseJwks=true, o JwtValidationMiddleware assume a validação RS256 via JWKS.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var supabaseUrl = builder.Configuration["Supabase:Url"]
            ?? throw new InvalidOperationException("Supabase:Url não configurado.");
        var jwtSecret = builder.Configuration["Supabase:JwtSecret"];

        options.Authority = $"{supabaseUrl}/auth/v1";
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            IssuerSigningKey = string.IsNullOrWhiteSpace(jwtSecret)
                ? null
                : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuerSigningKey = !string.IsNullOrWhiteSpace(jwtSecret)
        };

        // Mensagens claras de erro 401 para o frontend.
        options.Events = new JwtBearerEvents
        {
            OnChallenge = ctx =>
            {
                ctx.HandleResponse();
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                ctx.Response.ContentType = "application/json";
                var msg = ctx.AuthenticateFailure switch
                {
                    SecurityTokenExpiredException => "Token expirado. Renove a sessão.",
                    SecurityTokenInvalidSignatureException => "Assinatura inválida.",
                    null => "Autenticação obrigatória.",
                    _ => "Token inválido."
                };
                return ctx.Response.WriteAsJsonAsync(new { error = "unauthorized", message = msg });
            }
        };
    });

builder.Services.AddAuthorization();

// ─── SignalR ───────────────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ─── CORS — origens explícitas em allowlist ────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("VcaFrontend", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? ["http://localhost:5173", "https://vintagecodeacademy.vercel.app"];
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // necessário para SignalR + cookies de refresh
    });
});

// ─── Rate Limiting (.NET 8 built-in) ───────────────────────────────────────
// Política de partição por IP (auth) e por userId autenticado (quizzes/admin).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (ctx, _) =>
    {
        ctx.HttpContext.Response.ContentType = "application/json";
        await ctx.HttpContext.Response.WriteAsJsonAsync(new
        {
            error = "rate_limited",
            message = "Limite de requisições excedido. Tente novamente em alguns instantes."
        });
    };

    // Login: 5 tentativas / IP / minuto — defesa anti brute-force.
    options.AddPolicy("auth-login", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: GetClientIp(ctx),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    // Register: 3 / IP / hora — defesa anti spam de contas.
    options.AddPolicy("auth-register", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: GetClientIp(ctx),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    // Quiz submit: 10 / usuário / hora — anti-bot e antiabuse de pontuação.
    options.AddPolicy("quiz-submit", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: GetUserKeyOrIp(ctx),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    // Admin: 60 / usuário / minuto.
    options.AddPolicy("admin", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: GetUserKeyOrIp(ctx),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    // Limite global de fallback — defesa contra DoS básico.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: GetClientIp(ctx),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 300,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    static string GetClientIp(HttpContext ctx) =>
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    static string GetUserKeyOrIp(HttpContext ctx)
    {
        var sub = ctx.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
               ?? ctx.User?.FindFirst("sub")?.Value;
        return string.IsNullOrEmpty(sub) ? "ip:" + GetClientIp(ctx) : "user:" + sub;
    }
});

// ─── Camadas ───────────────────────────────────────────────────────────────
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddScoped<IRankingBroadcaster, SignalRRankingBroadcaster>();

var app = builder.Build();

// ─── Pipeline ──────────────────────────────────────────────────────────────
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseVcaSecurityHeaders();

// HTTPS obrigatório fora de Development.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.UseHttpsRedirection();

// Swagger — apenas em ambientes não-Production (defesa contra exposição de schema).
if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("VcaFrontend");

app.UseAuthentication();
// JWKS validation middleware (RS256) — ativado por configuração; idempotente caso usuário já autenticado.
app.UseMiddleware<JwtValidationMiddleware>();
app.UseAuthorization();

// Extrai userId após validação do JWT.
app.UseMiddleware<ExtractUserIdMiddleware>();

app.UseRateLimiter();

app.MapControllers();
app.MapHub<RankingHub>("/hubs/ranking");

app.Run();

/// <summary>Tornar Program parcial para testes de integração.</summary>
public partial class Program { }
