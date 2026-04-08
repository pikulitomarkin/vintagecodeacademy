using FluentAssertions;
using VCA.Domain.Entities;
using VCA.Infrastructure.Repositories;
using VCA.IntegrationTests.Infrastructure;

namespace VCA.IntegrationTests.Repositories;

/// <summary>
/// Testes de integração para UserRepository usando PostgreSQL via TestContainers.
/// </summary>
[Collection("PostgresCollection")]
public class UserRepositoryTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public UserRepositoryTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task GetByEmailAsync_WhenUserExists_ShouldReturnUser()
    {
        var email = $"test-{Guid.NewGuid()}@test.com";
        var user = User.Create(Guid.NewGuid(), email, "Test User");

        var repo = new UserRepository(_fixture.DbContext);
        await repo.AddAsync(user);
        await _fixture.DbContext.SaveChangesAsync();

        var found = await repo.GetByEmailAsync(email);
        found.Should().NotBeNull();
        found!.Email.Should().Be(email);
    }

    [Fact]
    public async Task GetTopByXpAsync_ShouldReturnUsersOrderedByXp()
    {
        var repo = new UserRepository(_fixture.DbContext);

        var user1 = User.Create(Guid.NewGuid(), $"u1-{Guid.NewGuid()}@test.com", "User 1");
        user1.AddXp(100);
        var user2 = User.Create(Guid.NewGuid(), $"u2-{Guid.NewGuid()}@test.com", "User 2");
        user2.AddXp(500);

        await repo.AddAsync(user1);
        await repo.AddAsync(user2);
        await _fixture.DbContext.SaveChangesAsync();

        var top = await repo.GetTopByXpAsync(1);
        top.Should().HaveCount(1);
        top[0].Xp.Should().BeGreaterThanOrEqualTo(500);
    }
}
