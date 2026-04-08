using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using VCA.Infrastructure.Data;

namespace VCA.IntegrationTests.Infrastructure;

/// <summary>
/// Fixture que sobe um container PostgreSQL para testes de integração.
/// Usa TestContainers para garantir isolamento e reprodutibilidade.
/// </summary>
public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("vca_test")
        .WithUsername("postgres")
        .WithPassword("postgres_test")
        .Build();

    public AppDbContext DbContext { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;

        DbContext = new AppDbContext(options);
        await DbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await DbContext.DisposeAsync();
        await _container.StopAsync();
    }
}
