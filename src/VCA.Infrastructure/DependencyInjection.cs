using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

        // DeepSeek
        services.AddHttpClient<IDeepSeekService, DeepSeekService>();

        // Resend (email)
        services.AddHttpClient<IEmailService, ResendEmailService>();

        // Supabase Storage
        services.AddHttpClient<IStorageService, SupabaseStorageService>();

        // PDF Extractor (sem HTTP)
        services.AddSingleton<IPdfExtractorService, PdfExtractorService>();

        return services;
    }
}
