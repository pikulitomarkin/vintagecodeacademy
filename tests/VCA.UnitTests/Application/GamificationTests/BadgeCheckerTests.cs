using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VCA.Application.Gamification;
using VCA.Domain.Entities;
using VCA.Domain.Enums;
using VCA.Domain.Interfaces;

namespace VCA.UnitTests.Application.GamificationTests;

/// <summary>
/// Concessão de cada badge nas condições exatas e idempotência (sem duplicação).
/// </summary>
public class BadgeCheckerTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IBadgeRepository> _badges = new();
    private readonly Mock<ILabApplicationRepository> _labApps = new();
    private readonly Mock<IRankingRepository> _rankings = new();
    private readonly Mock<IUserProgressRepository> _progresses = new();
    private readonly Mock<ILessonRepository> _lessons = new();
    private readonly Mock<IModuleRepository> _modules = new();
    private readonly Mock<ITrailRepository> _trails = new();
    private readonly Mock<IQuizAttemptRepository> _quizAttempts = new();

    private readonly Guid _userId = Guid.NewGuid();
    private readonly User _user;

    public BadgeCheckerTests()
    {
        _uow.Setup(u => u.Users).Returns(_users.Object);
        _uow.Setup(u => u.Badges).Returns(_badges.Object);
        _uow.Setup(u => u.LabApplications).Returns(_labApps.Object);
        _uow.Setup(u => u.Rankings).Returns(_rankings.Object);
        _uow.Setup(u => u.UserProgresses).Returns(_progresses.Object);
        _uow.Setup(u => u.Lessons).Returns(_lessons.Object);
        _uow.Setup(u => u.Modules).Returns(_modules.Object);
        _uow.Setup(u => u.Trails).Returns(_trails.Object);
        _uow.Setup(u => u.QuizAttempts).Returns(_quizAttempts.Object);

        _user = User.Create(_userId, "u@u.com", "U");
        _users.Setup(r => r.GetByIdAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync(_user);

        // defaults vazios
        _badges.Setup(r => r.GetByUserAsync(_userId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(Array.Empty<Badge>());
        _labApps.Setup(r => r.GetByUserAsync(_userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<LabApplication>());
        _progresses.Setup(r => r.GetByUserAsync(_userId, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(Array.Empty<UserProgress>());
        _quizAttempts.Setup(r => r.GetByUserAsync(_userId, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(Array.Empty<QuizAttempt>());
        _trails.Setup(r => r.GetPublishedAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(Array.Empty<Trail>());
        _rankings.Setup(r => r.GetByUserAndWeekAsync(_userId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((Ranking?)null);

        // Default: badge existe
        _badges.Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync((string code, CancellationToken _) =>
                   Badge.Create(code, code, code, 0));
    }

    private BadgeCheckerService Build() =>
        new(_uow.Object, NullLogger<BadgeCheckerService>.Instance);

    private void AssertGranted(string code, Times times) =>
        _badges.Verify(r => r.GrantToUserAsync(_userId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            times);

    // ── OnFire ────────────────────────────────────────────────────────────────
    [Fact]
    public async Task OnFire_GrantedWhenStreakReaches30()
    {
        SetUserStreak(_user, 30);
        await Build().CheckAndGrantBadgesAsync(_userId);
        _badges.Verify(r => r.GrantToUserAsync(_userId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task OnFire_NotGrantedWhenStreakBelow30()
    {
        SetUserStreak(_user, 29);
        await Build().CheckAndGrantBadgesAsync(_userId);
        _badges.Verify(r => r.GrantToUserAsync(_userId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── FirstDeploy / VintageContributor ──────────────────────────────────────
    [Fact]
    public async Task FirstDeployAndVintageContributor_GrantedOnAcceptedLabApp()
    {
        var app = LabApplication.Create(_userId, Guid.NewGuid());
        app.Accept();
        _labApps.Setup(r => r.GetByUserAsync(_userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { app });

        await Build().CheckAndGrantBadgesAsync(_userId);

        // Dois badges concedidos: first_deploy e vintage_contributor
        _badges.Verify(r => r.GrantToUserAsync(_userId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task FirstDeploy_NotGrantedWhenAllAppsPending()
    {
        var app = LabApplication.Create(_userId, Guid.NewGuid());
        _labApps.Setup(r => r.GetByUserAsync(_userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { app });

        await Build().CheckAndGrantBadgesAsync(_userId);
        _badges.Verify(r => r.GrantToUserAsync(_userId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── TopDev ────────────────────────────────────────────────────────────────
    [Fact]
    public async Task TopDev_GrantedWhenPositionInTop3()
    {
        var ranking = Ranking.Create(_userId, 202401, 1000, position: 2);
        _rankings.Setup(r => r.GetByUserAndWeekAsync(_userId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(ranking);

        await Build().CheckAndGrantBadgesAsync(_userId);
        _badges.Verify(r => r.GrantToUserAsync(_userId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task TopDev_NotGrantedWhenPositionAboveTop3()
    {
        var ranking = Ranking.Create(_userId, 202401, 100, position: 4);
        _rankings.Setup(r => r.GetByUserAndWeekAsync(_userId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(ranking);

        await Build().CheckAndGrantBadgesAsync(_userId);
        AssertGranted("top_dev", Times.Never());
    }

    // ── QuizMaster ────────────────────────────────────────────────────────────
    [Fact]
    public async Task QuizMaster_GrantedWhenLast10AttemptsAllPerfect()
    {
        var attempts = Enumerable.Range(0, 10)
            .Select(_ => QuizAttempt.Create(_userId, Guid.NewGuid(), 5, "[]"))
            .ToList();
        _quizAttempts.Setup(r => r.GetByUserAsync(_userId, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(attempts);

        await Build().CheckAndGrantBadgesAsync(_userId);
        _badges.Verify(r => r.GrantToUserAsync(_userId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task QuizMaster_NotGrantedWhenSomeAttemptsImperfect()
    {
        var attempts = Enumerable.Range(0, 10)
            .Select(i => QuizAttempt.Create(_userId, Guid.NewGuid(), i == 0 ? 4 : 5, "[]"))
            .ToList();
        _quizAttempts.Setup(r => r.GetByUserAsync(_userId, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(attempts);

        await Build().CheckAndGrantBadgesAsync(_userId);
        AssertGranted("quiz_master", Times.Never());
    }

    [Fact]
    public async Task QuizMaster_NotGrantedWithFewerThan10Attempts()
    {
        var attempts = Enumerable.Range(0, 9)
            .Select(_ => QuizAttempt.Create(_userId, Guid.NewGuid(), 5, "[]"))
            .ToList();
        _quizAttempts.Setup(r => r.GetByUserAsync(_userId, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(attempts);

        await Build().CheckAndGrantBadgesAsync(_userId);
        AssertGranted("quiz_master", Times.Never());
    }

    // ── Idempotência ──────────────────────────────────────────────────────────
    [Fact]
    public async Task Badge_NotGrantedTwiceWhenAlreadyEarned()
    {
        SetUserStreak(_user, 30);
        var existing = Badge.Create("on_fire", "On Fire", "Streak 30+", 0);
        _badges.Setup(r => r.GetByUserAsync(_userId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(new[] { existing });

        await Build().CheckAndGrantBadgesAsync(_userId);

        _badges.Verify(r => r.GrantToUserAsync(_userId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Helper: streak via reflexão (User.StreakDays é private setter) ────────
    private static void SetUserStreak(User user, int days)
    {
        for (int i = 0; i < days; i++) user.IncrementStreak();
    }
}
