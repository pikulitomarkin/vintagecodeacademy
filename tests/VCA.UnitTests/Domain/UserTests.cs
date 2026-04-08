using FluentAssertions;
using VCA.Domain.Entities;
using VCA.Domain.Enums;

namespace VCA.UnitTests.Domain;

/// <summary>
/// Testes unitários para a entidade User — lógica de domínio, XP e níveis.
/// </summary>
public class UserTests
{
    [Fact]
    public void Create_ShouldInitializeWithDefaultValues()
    {
        var userId = Guid.NewGuid();
        var user = User.Create(userId, "dev@example.com", "Dev User");

        user.Id.Should().Be(userId);
        user.Email.Should().Be("dev@example.com");
        user.Xp.Should().Be(0);
        user.Level.Should().Be(UserLevel.Rookie);
        user.StreakDays.Should().Be(0);
    }

    [Theory]
    [InlineData(0, UserLevel.Rookie)]
    [InlineData(499, UserLevel.Rookie)]
    [InlineData(500, UserLevel.Apprentice)]
    [InlineData(1499, UserLevel.Apprentice)]
    [InlineData(1500, UserLevel.Builder)]
    [InlineData(3999, UserLevel.Builder)]
    [InlineData(4000, UserLevel.Craftsman)]
    [InlineData(9999, UserLevel.Craftsman)]
    [InlineData(10000, UserLevel.Expert)]
    [InlineData(24999, UserLevel.Expert)]
    [InlineData(25000, UserLevel.VintageDev)]
    public void AddXp_ShouldUpdateLevelCorrectly(int xp, UserLevel expectedLevel)
    {
        var user = User.Create(Guid.NewGuid(), "test@test.com", "Test");
        user.AddXp(xp);

        user.Xp.Should().Be(xp);
        user.Level.Should().Be(expectedLevel);
    }

    [Fact]
    public void AddXp_WithZeroAmount_ShouldNotChangeXp()
    {
        var user = User.Create(Guid.NewGuid(), "test@test.com", "Test");
        user.AddXp(0);

        user.Xp.Should().Be(0);
    }

    [Fact]
    public void AddXp_WithNegativeAmount_ShouldNotChangeXp()
    {
        var user = User.Create(Guid.NewGuid(), "test@test.com", "Test");
        user.AddXp(100);
        user.AddXp(-50);

        user.Xp.Should().Be(100);
    }

    [Fact]
    public void IncrementStreak_ShouldIncreaseStreakByOne()
    {
        var user = User.Create(Guid.NewGuid(), "test@test.com", "Test");
        user.IncrementStreak();
        user.IncrementStreak();

        user.StreakDays.Should().Be(2);
    }

    [Fact]
    public void ResetStreak_ShouldSetStreakToZero()
    {
        var user = User.Create(Guid.NewGuid(), "test@test.com", "Test");
        user.IncrementStreak();
        user.IncrementStreak();
        user.ResetStreak();

        user.StreakDays.Should().Be(0);
    }
}
