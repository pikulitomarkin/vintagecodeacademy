using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using VCA.Application.Gamification;
using VCA.Application.Gamification.CompleteLesson;
using VCA.Application.Gamification.Commands;
using VCA.Application.Interfaces;
using VCA.Domain.Entities;
using VCA.Domain.Enums;
using VCA.Domain.Interfaces;

namespace VCA.UnitTests.Application.GamificationTests;

/// <summary>
/// Concessão de XP por evento, transições de nível nos thresholds exatos e
/// idempotência de XP por evento (via UserProgress para LessonCompleted).
/// </summary>
public class XpAwardTests
{
    // ── XP por evento ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(XpReason.LessonCompleted, XpEvents.LessonCompleted)]
    [InlineData(XpReason.QuickChallengeCompleted, XpEvents.QuickChallengeCompleted)]
    [InlineData(XpReason.QuizPerfectScore, XpEvents.QuizPerfectScore)]
    [InlineData(XpReason.WeekStreak, XpEvents.WeekStreak)]
    [InlineData(XpReason.ProjectDelivered, XpEvents.ProjectDelivered)]
    [InlineData(XpReason.WeeklyRankingTop3, XpEvents.WeeklyRankingTop3)]
    [InlineData(XpReason.ForumHelpful, XpEvents.ForumHelpful)]
    [InlineData(XpReason.DailyLogin, XpEvents.DailyLogin)]
    public void XpEvents_ForReason_ShouldReturnExpectedXp(XpReason reason, int expected)
    {
        XpEvents.ForReason(reason).Should().Be(expected);
    }

    [Fact]
    public void XpEvents_ForUnknownReason_ShouldReturnZero()
    {
        XpEvents.ForReason((XpReason)99).Should().Be(0);
    }

    // ── Thresholds de nível ───────────────────────────────────────────────────

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
    public void LevelThresholds_FromXp_ShouldReturnExactLevel(int xp, UserLevel expected)
    {
        LevelThresholds.FromXp(xp).Should().Be(expected);
    }

    [Theory]
    [InlineData(UserLevel.Rookie,     500)]
    [InlineData(UserLevel.Apprentice, 1500)]
    [InlineData(UserLevel.Builder,    4000)]
    [InlineData(UserLevel.Craftsman,  10000)]
    [InlineData(UserLevel.Expert,     25000)]
    [InlineData(UserLevel.VintageDev, int.MaxValue)]
    public void LevelThresholds_NextThreshold_ShouldReturnExpectedValue(UserLevel level, int expected)
    {
        LevelThresholds.NextThreshold(level).Should().Be(expected);
    }

    // ── Handler: AwardXp ───────────────────────────────────────────────────────

    [Fact]
    public async Task AwardXp_ShouldGrantCorrectAmountAndUpdateLevelOnExactThreshold()
    {
        var userId = Guid.NewGuid();
        // Inicia em 490 XP (Rookie). Award LessonCompleted (+10) → 500 XP → Apprentice.
        var user = User.Create(userId, "u@u.com", "U");
        user.AddXp(490);

        var (handler, uow, _) = BuildHandler(user);

        var result = await handler.Handle(
            new AwardXpCommand(userId, XpReason.LessonCompleted), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.XpAwarded.Should().Be(XpEvents.LessonCompleted);
        result.Value.NewTotalXp.Should().Be(500);
        result.Value.PreviousLevel.Should().Be(nameof(UserLevel.Rookie));
        result.Value.NewLevel.Should().Be(nameof(UserLevel.Apprentice));
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task AwardXp_BelowThreshold_ShouldNotChangeLevel()
    {
        var userId = Guid.NewGuid();
        var user = User.Create(userId, "u@u.com", "U");
        user.AddXp(100);

        var (handler, _, _) = BuildHandler(user);

        var result = await handler.Handle(
            new AwardXpCommand(userId, XpReason.LessonCompleted), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.NewLevel.Should().BeNull();
        result.Value.PreviousLevel.Should().Be(nameof(UserLevel.Rookie));
    }

    [Fact]
    public async Task AwardXp_WhenUserNotFound_ShouldFail()
    {
        var (handler, _, _) = BuildHandler(null);
        var result = await handler.Handle(
            new AwardXpCommand(Guid.NewGuid(), XpReason.LessonCompleted), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    // ── Idempotência: mesma aula não pode conceder XP duas vezes ──────────────

    [Fact]
    public async Task CompleteLesson_WhenLessonAlreadyCompleted_ShouldNotGrantDuplicateXp()
    {
        var userId = Guid.NewGuid();
        var lessonId = Guid.NewGuid();
        var user = User.Create(userId, "u@u.com", "U");

        var uow = new Mock<IUnitOfWork>();
        var users = new Mock<IUserRepository>();
        var lessons = new Mock<ILessonRepository>();
        var progress = new Mock<IUserProgressRepository>();

        uow.Setup(u => u.Users).Returns(users.Object);
        uow.Setup(u => u.Lessons).Returns(lessons.Object);
        uow.Setup(u => u.UserProgresses).Returns(progress.Object);

        // Já completou — segunda tentativa deve falhar
        progress.Setup(r => r.HasCompletedAsync(userId, lessonId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

        var handler = new CompleteLessonHandler(uow.Object, NullLogger<CompleteLessonHandler>());
        var result = await handler.HandleAsync(new CompleteLessonCommand(userId, lessonId));

        result.IsFailure.Should().BeTrue();
        user.Xp.Should().Be(0); // XP não alterado
        progress.Verify(r => r.AddAsync(It.IsAny<UserProgress>(), It.IsAny<CancellationToken>()), Times.Never);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (AwardXpCommandHandler handler, Mock<IUnitOfWork> uow, Mock<IPublisher> pub)
        BuildHandler(User? user)
    {
        var uow = new Mock<IUnitOfWork>();
        var users = new Mock<IUserRepository>();
        var rankings = new Mock<IRankingRepository>();

        uow.Setup(u => u.Users).Returns(users.Object);
        uow.Setup(u => u.Rankings).Returns(rankings.Object);
        users.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(user);
        rankings.Setup(r => r.GetByUserAndWeekAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Ranking?)null);
        rankings.Setup(r => r.GetWeeklyTopAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<Ranking>());

        var broadcaster = new Mock<IRankingBroadcaster>();
        var ranking = new RankingUpdaterService(uow.Object, broadcaster.Object,
            NullLogger<RankingUpdaterService>());

        var pub = new Mock<IPublisher>();
        var handler = new AwardXpCommandHandler(
            uow.Object, pub.Object, ranking, NullLogger<AwardXpCommandHandler>());

        return (handler, uow, pub);
    }

    private static ILogger<T> NullLogger<T>() =>
        Microsoft.Extensions.Logging.Abstractions.NullLogger<T>.Instance;
}
