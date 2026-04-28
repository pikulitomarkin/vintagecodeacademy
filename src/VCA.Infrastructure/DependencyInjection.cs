using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using VCA.Application.Interfaces;
using VCA.Domain.Interfaces;
using VCA.Infrastructure.Data;
using VCA.Infrastructure.ExternalServices;
using VCA.Infrastructure.Repositories;

namespace VCA.Infrastructure;

/// <summary>
/// Extensões de injeção de dependência para a camada Infrastructure.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // EF Core + PostgreSQL
        var connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' não encontrada.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

        // Unit of Work e Repositórios
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // DeepSeek — implementação legada (compatibilidade)
        services.AddHttpClient<IDeepSeekService, DeepSeekService>()
            .AddPolicyHandler(GetDeepSeekRetryPolicy());

        // DeepSeek — novo cliente robusto (VCA Intelligence)
        services.AddHttpClient<IAiCompletionClient, DeepSeekApiClient>()
            .AddPolicyHandler(GetDeepSeekRetryPolicy());

        // Supabase Auth — wrapper REST
        services.AddHttpClient<ISupabaseAuthService, SupabaseAuthService>();

        // Sincronização de usuário Supabase ↔ banco local
        services.AddScoped<IUserSyncService, Auth.UserSyncService>();

        // Resend (email)
        services.AddHttpClient<IEmailService, ResendEmailService>();

        // Supabase Storage
        services.AddHttpClient<IStorageService, SupabaseStorageService>();

        // PDF Extractor (sem HTTP)
        services.AddSingleton<IPdfExtractorService, PdfExtractorService>();

        return services;
    }

    /// <summary>
    /// Retry com backoff exponencial: 3 tentativas em 5xx, 408 e 429.
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> GetDeepSeekRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(r => (int)r.StatusCode == 429)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (outcome, delay, attempt, ctx) =>
                {
                    var logger = ctx.GetLogger();
                    logger?.LogWarning(
                        "Retry {Attempt} após {Delay}s. Status={Status} Exception={Ex}",
                        attempt, delay.TotalSeconds,
                        (int?)outcome.Result?.StatusCode, outcome.Exception?.Message);
                });
    }
}

internal static class PollyContextExtensions
{
    private const string LoggerKey = "ILogger";

    public static Context WithLogger(this Context ctx, ILogger logger)
    {
        ctx[LoggerKey] = logger;
        return ctx;
    }

    public static ILogger? GetLogger(this Context ctx) =>
        ctx.TryGetValue(LoggerKey, out var v) && v is ILogger l ? l : null;
}
