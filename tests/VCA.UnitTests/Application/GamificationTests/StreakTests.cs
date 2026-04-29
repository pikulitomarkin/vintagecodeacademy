using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VCA.Application.Gamification;
using VCA.Application.Gamification.Commands;
using VCA.Application.Interfaces;
using VCA.Domain.Entities;
using VCA.Domain.Interfaces;

namespace VCA.UnitTests.Application.GamificationTests;

/// <summary>
/// Streak: incremento em dias consecutivos, reset em gap >= 2 dias e bônus em 7/30.
/// </summary>
public class StreakTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IRankingRepository> _rankings = new();
    private readonly Mock<IRankingBroadcaster> _broadcaster = new();
    private readonly Guid _userId = Guid.NewGuid();

    public StreakTests()
    {
        _uow.Setup(u => u.Users).Returns(_users.Object);
        _uow.Setup(u => u.Rankings).Returns(_rankings.Object);
        _rankings.Setup(r => r.GetByUserAndWeekAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((Ranking?)null);
        _rankings.Setup(r => r.GetWeeklyTopAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Array.Empty<Ranking>());
    }

    private UpdateStreakCommandHandler Build()
    {
        var ranking = new RankingUpdaterService(_uow.Object, _broadcaster.Object,
            NullLogger<RankingUpdaterService>.Instance);
        return new UpdateStreakCommandHandler(_uow.Object, ranking,
            NullLogger<UpdateStreakCommandHandler>.Instance);
    }

    private User SetupUser(int initialStreak)
    {
        var user = User.Create(_userId, "u@u.com", "U");
        for (int i = 0; i < initialStreak; i++) user.IncrementStreak();
        _users.Setup(r => r.GetByIdAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        return user;
    }

    [Fact]
    public async Task Streak_IncrementsWhenLastActivityWasYesterday()
    {
        var user = SetupUser(initialStreak: 3);
        var yesterday = DateTime.UtcNow.Date.AddDays(-1).AddHours(10);

        var result = await Build().Handle(
            new UpdateStreakCommand(_userId, yesterday), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Incremented.Should().BeTrue();
        result.Value.StreakDays.Should().Be(4);
        user.StreakDays.Should().Be(4);
    }

    [Fact]
    public async Task Streak_ResetsToOneWhenGapGreaterThan1Day()
    {
        var user = SetupUser(initialStreak: 10);
        var threeDaysAgo = DateTime.UtcNow.Date.AddDays(-3);

        var result = await Build().Handle(
            new UpdateStreakCommand(_userId, threeDaysAgo), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Incremented.Should().BeFalse();
        result.Value.StreakDays.Should().Be(1);
        user.StreakDays.Should().Be(1);
    }

    [Fact]
    public async Task Streak_DoesNotChangeWhenAlreadyVisitedToday()
    {
        var user = SetupUser(initialStreak: 5);
        var today = DateTime.UtcNow.Date.AddHours(8);

        var result = await Build().Handle(
            new UpdateStreakCommand(_userId, today), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Incremented.Should().BeFalse();
        result.Value.StreakDays.Should().Be(5);
        user.StreakDays.Should().Be(5);
    }

    [Fact]
    public async Task Streak_StartsAtOneWhenNoPreviousActivity()
    {
        var user = SetupUser(initialStreak: 0);

        var result = await Build().Handle(
            new UpdateStreakCommand(_userId, null), CancellationToken.None);

        result.Value!.StreakDays.Should().Be(1);
        user.StreakDays.Should().Be(1);
    }

    [Fact]
    public async Task Streak_AwardsBonusXpAt7Days()
    {
        var user = SetupUser(initialStreak: 6);
        var yesterday = DateTime.UtcNow.Date.AddDays(-1);

        var result = await Build().Handle(
            new UpdateStreakCommand(_userId, yesterday), CancellationToken.None);

        result.Value!.StreakDays.Should().Be(7);
        result.Value.BonusXpAwarded.Should().Be(XpEvents.WeekStreak);
        user.Xp.Should().Be(XpEvents.WeekStreak);
    }

    [Fact]
    public async Task Streak_AwardsBonusXpAt30Days()
    {
        var user = SetupUser(initialStreak: 29);
        var yesterday = DateTime.UtcNow.Date.AddDays(-1);

        var result = await Build().Handle(
            new UpdateStreakCommand(_userId, yesterday), CancellationToken.None);

        result.Value!.StreakDays.Should().Be(30);
        result.Value.BonusXpAwarded.Should().Be(150);
        user.Xp.Should().Be(150);
    }

    [Fact]
    public async Task Streak_NoBonusOnNonMilestoneDay()
    {
        var user = SetupUser(initialStreak: 4);
        var yesterday = DateTime.UtcNow.Date.AddDays(-1);

        var result = await Build().Handle(
            new UpdateStreakCommand(_userId, yesterday), CancellationToken.None);

        result.Value!.StreakDays.Should().Be(5);
        result.Value.BonusXpAwarded.Should().Be(0);
        user.Xp.Should().Be(0);
    }
}
