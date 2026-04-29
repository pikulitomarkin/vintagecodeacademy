using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using VCA.Application.Interfaces;
using VCA.Infrastructure.Data;
using VCA.IntegrationTests.Infrastructure.Fakes;

namespace VCA.IntegrationTests.Infrastructure;

/// <summary>
/// WebApplicationFactory configurada para testes:
///   - Sobe PostgreSQL via TestContainers.
///   - Substitui serviços externos (Supabase, IA, PDF, e-mail, storage) por fakes.
///   - Substitui o JwtBearer pelo TestAuthHandler — autenticação via headers.
/// </summary>
public class VcaWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("vca_int_test")
        .WithUsername("postgres")
        .WithPassword("postgres_test")
        .Build();

    public FakeSupabaseAuthService Supabase { get; } = new();
    public FakeAiCompletionClient AiClient { get; } = new();
    public FakePdfExtractor PdfExtractor { get; } = new();
    public FakeEmailService Email { get; } = new();
    public FakeStorageService Storage { get; } = new();
    public FakeRankingBroadcaster Broadcaster { get; } = new();
    public FakeDeepSeekService DeepSeek { get; } = new();

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // Cria o schema antes do primeiro request (EnsureCreated, sem migrations).
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // O ambiente "Development" desativa HSTS/HTTPS-only e simplifica o pipeline.
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((ctx, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString(),
                ["Supabase:Url"] = "https://fake.supabase.co",
                ["Supabase:JwtSecret"] = "test-secret-test-secret-test-secret-1234567890",
                ["Supabase:UseJwks"] = "false",
                ["DeepSeek:ApiKey"] = "fake",
                ["Resend:ApiKey"] = "fake",
                ["Sentry:Dsn"] = "",
                ["Cors:AllowedOrigins:0"] = "http://localhost"
            });
        });

        builder.ConfigureServices(services =>
        {
            // ── Substitui serviços externos por fakes ───────────────────────────
            services.RemoveAll<ISupabaseAuthService>();
            services.AddSingleton<ISupabaseAuthService>(Supabase);

            services.RemoveAll<IAiCompletionClient>();
            services.AddSingleton<IAiCompletionClient>(AiClient);

            services.RemoveAll<IPdfExtractorService>();
            services.AddSingleton<IPdfExtractorService>(PdfExtractor);

            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailService>(Email);

            services.RemoveAll<IStorageService>();
            services.AddSingleton<IStorageService>(Storage);

            services.RemoveAll<IRankingBroadcaster>();
            services.AddSingleton<IRankingBroadcaster>(Broadcaster);

            services.RemoveAll<IDeepSeekService>();
            services.AddSingleton<IDeepSeekService>(DeepSeek);

            services.RemoveAll<IUserSyncService>();
            services.AddScoped<IUserSyncService, FakeUserSyncService>();

            // ── Autenticação de teste ───────────────────────────────────────────
            // Substitui o esquema padrão JwtBearer pelo TestAuthHandler (via headers).
            services.PostConfigure<AuthenticationOptions>(opts =>
            {
                opts.DefaultScheme = TestAuthHandler.SchemeName;
                opts.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                opts.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            });
            services.AddAuthentication(TestAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        TestAuthHandler.SchemeName, _ => { });
        });
    }

    public AppDbContext CreateDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }
}

[CollectionDefinition("Vca")]
public class VcaCollection : ICollectionFixture<VcaWebApplicationFactory> { }
