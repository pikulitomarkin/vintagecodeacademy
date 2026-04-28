using System.Globalization;
using Microsoft.Extensions.Logging;
using VCA.Domain.Entities;
using VCA.Domain.Enums;
using VCA.Domain.Interfaces;

namespace VCA.Application.Gamification;

/// <summary>
/// Serviço que verifica e concede badges automaticamente após eventos de XP.
/// Badges já conquistados são ignorados (idempotente).
/// </summary>
public class BadgeCheckerService
{
    // Deve coincidir com QuestionsPerAttempt em SubmitQuizHandler
    private const int QuizQuestionsPerAttempt = 5;

    private readonly IUnitOfWork _uow;
    private readonly ILogger<BadgeCheckerService> _logger;

    public BadgeCheckerService(IUnitOfWork uow, ILogger<BadgeCheckerService> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    /// <summary>
    /// Verifica todos os badges possíveis para o usuário e concede os elegíveis.
    /// </summary>
    public async Task CheckAndGrantBadgesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _uow.Users.GetByIdAsync(userId, cancellationToken);
        if (user is null) return;

        // Carrega badges já conquistados para evitar concessão duplicada
        var earnedBadgeCodes = (await _uow.Badges.GetByUserAsync(userId, cancellationToken))
            .Select(b => b.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // — OnFire: streak >= 30 dias —
        await CheckOnFireAsync(user, earnedBadgeCodes, cancellationToken);

        // — FirstDeploy e VintageContributor: lab applications aprovadas —
        var labApplications = await _uow.LabApplications.GetByUserAsync(userId, cancellationToken);
        await CheckFirstDeployAsync(user, labApplications, earnedBadgeCodes, cancellationToken);
        await CheckVintageContributorAsync(user, labApplications, earnedBadgeCodes, cancellationToken);

        // — TopDev: top 3 no ranking da semana atual —
        await CheckTopDevAsync(user, earnedBadgeCodes, cancellationToken);

        // — KnowledgeSeeker e SpeedRunner: requerem dados de trilha —
        var userProgress = await _uow.UserProgresses.GetByUserAsync(userId, cancellationToken);
        if (userProgress.Count > 0)
        {
            var lessonIds = userProgress.Select(p => p.LessonId).ToHashSet();
            var lessons = await _uow.Lessons.FindAsync(l => lessonIds.Contains(l.Id), cancellationToken);

            var moduleIds = lessons.Select(l => l.ModuleId).ToHashSet();
            var modules = await _uow.Modules.FindAsync(m => moduleIds.Contains(m.Id), cancellationToken);

            var publishedTrails = await _uow.Trails.GetPublishedAsync(cancellationToken);

            await CheckKnowledgeSeekerAsync(user, userProgress, lessons, modules, publishedTrails, earnedBadgeCodes, cancellationToken);
            await CheckSpeedRunnerAsync(user, userProgress, lessons, modules, publishedTrails, earnedBadgeCodes, cancellationToken);
        }

        // — QuizMaster: 10 tentativas consecutivas com score perfeito —
        var quizAttempts = await _uow.QuizAttempts.GetByUserAsync(userId, cancellationToken);
        await CheckQuizMasterAsync(user, quizAttempts, earnedBadgeCodes, cancellationToken);

        await _uow.SaveChangesAsync(cancellationToken);
    }

    // ── Verificações individuais ───────────────────────────────────────────────

    private async Task CheckOnFireAsync(
        User user, HashSet<string> earned, CancellationToken ct)
    {
        if (earned.Contains(BadgeCodes.OnFire)) return;
        if (user.StreakDays >= 30)
            await GrantBadgeAsync(user.Id, BadgeCodes.OnFire, earned, ct);
    }

    private async Task CheckFirstDeployAsync(
        User user, IReadOnlyList<LabApplication> applications,
        HashSet<string> earned, CancellationToken ct)
    {
        if (earned.Contains(BadgeCodes.FirstDeploy)) return;
        if (applications.Any(a => a.Status == LabApplicationStatus.Accepted))
            await GrantBadgeAsync(user.Id, BadgeCodes.FirstDeploy, earned, ct);
    }

    private async Task CheckVintageContributorAsync(
        User user, IReadOnlyList<LabApplication> applications,
        HashSet<string> earned, CancellationToken ct)
    {
        if (earned.Contains(BadgeCodes.VintageContributor)) return;
        if (applications.Any(a => a.Status == LabApplicationStatus.Accepted))
            await GrantBadgeAsync(user.Id, BadgeCodes.VintageContributor, earned, ct);
    }

    private async Task CheckTopDevAsync(
        User user, HashSet<string> earned, CancellationToken ct)
    {
        if (earned.Contains(BadgeCodes.TopDev)) return;

        var week = GetCurrentWeekNumber();
        var ranking = await _uow.Rankings.GetByUserAndWeekAsync(user.Id, week, ct);

        if (ranking is { Position: > 0 and <= 3 })
            await GrantBadgeAsync(user.Id, BadgeCodes.TopDev, earned, ct);
    }

    private async Task CheckKnowledgeSeekerAsync(
        User user,
        IReadOnlyList<UserProgress> userProgress,
        IReadOnlyList<Lesson> lessons,
        IReadOnlyList<Module> modules,
        IReadOnlyList<Trail> publishedTrails,
        HashSet<string> earned,
        CancellationToken ct)
    {
        if (earned.Contains(BadgeCodes.KnowledgeSeeker)) return;

        var completedLessonIds = userProgress.Select(p => p.LessonId).ToHashSet();
        var publishedTrailIds = publishedTrails.Select(t => t.Id).ToHashSet();

        var count = CountCompletedTrails(completedLessonIds, lessons, modules, publishedTrailIds);

        if (count >= 5)
            await GrantBadgeAsync(user.Id, BadgeCodes.KnowledgeSeeker, earned, ct);
    }

    private async Task CheckSpeedRunnerAsync(
        User user,
        IReadOnlyList<UserProgress> userProgress,
        IReadOnlyList<Lesson> lessons,
        IReadOnlyList<Module> modules,
        IReadOnlyList<Trail> publishedTrails,
        HashSet<string> earned,
        CancellationToken ct)
    {
        if (earned.Contains(BadgeCodes.SpeedRunner)) return;

        var completedLessonIds = userProgress.Select(p => p.LessonId).ToHashSet();
        var progressByLesson = userProgress.ToDictionary(p => p.LessonId, p => p.CompletedAt);
        var publishedTrailIds = publishedTrails.Select(t => t.Id).ToHashSet();

        var modulesByTrail = modules
            .Where(m => publishedTrailIds.Contains(m.TrailId))
            .GroupBy(m => m.TrailId)
            .ToDictionary(g => g.Key, g => g.Select(m => m.Id).ToHashSet());

        var lessonsByModule = lessons
            .GroupBy(l => l.ModuleId)
            .ToDictionary(g => g.Key, g => g.Select(l => l.Id).ToHashSet());

        foreach (var (trailId, trailModuleIds) in modulesByTrail)
        {
            var trailLessonIds = trailModuleIds
                .SelectMany(mid => lessonsByModule.TryGetValue(mid, out var ls)
                    ? ls
                    : Enumerable.Empty<Guid>())
                .ToHashSet();

            if (trailLessonIds.Count == 0) continue;
            if (!trailLessonIds.All(lid => completedLessonIds.Contains(lid))) continue;

            var completionDates = trailLessonIds
                .Where(progressByLesson.ContainsKey)
                .Select(lid => progressByLesson[lid])
                .ToList();

            if (completionDates.Count == 0) continue;

            var span = completionDates.Max() - completionDates.Min();
            if (span.TotalDays < 7)
            {
                await GrantBadgeAsync(user.Id, BadgeCodes.SpeedRunner, earned, ct);
                return;
            }
        }
    }

    private async Task CheckQuizMasterAsync(
        User user,
        IReadOnlyList<QuizAttempt> attempts,
        HashSet<string> earned,
        CancellationToken ct)
    {
        if (earned.Contains(BadgeCodes.QuizMaster)) return;
        if (attempts.Count < 10) return;

        var lastTen = attempts
            .OrderByDescending(a => a.AttemptedAt)
            .Take(10)
            .ToList();

        if (lastTen.All(a => a.Score >= QuizQuestionsPerAttempt))
            await GrantBadgeAsync(user.Id, BadgeCodes.QuizMaster, earned, ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task GrantBadgeAsync(
        Guid userId, string badgeCode, HashSet<string> earned, CancellationToken ct)
    {
        var badge = await _uow.Badges.GetByCodeAsync(badgeCode, ct);
        if (badge is null)
        {
            _logger.LogWarning(
                "Badge '{Code}' não encontrado na base de dados. Verifique os dados de seed.",
                badgeCode);
            return;
        }

        await _uow.Badges.GrantToUserAsync(userId, badge.Id, ct);
        earned.Add(badgeCode);

        _logger.LogInformation(
            "Badge '{Code}' concedido ao usuário {UserId}.", badgeCode, userId);
    }

    private static int CountCompletedTrails(
        HashSet<Guid> completedLessonIds,
        IReadOnlyList<Lesson> lessons,
        IReadOnlyList<Module> modules,
        HashSet<Guid> publishedTrailIds)
    {
        var modulesByTrail = modules
            .Where(m => publishedTrailIds.Contains(m.TrailId))
            .GroupBy(m => m.TrailId)
            .ToDictionary(g => g.Key, g => g.Select(m => m.Id).ToHashSet());

        var lessonsByModule = lessons
            .GroupBy(l => l.ModuleId)
            .ToDictionary(g => g.Key, g => g.Select(l => l.Id).ToHashSet());

        int count = 0;
        foreach (var (_, trailModuleIds) in modulesByTrail)
        {
            var trailLessonIds = trailModuleIds
                .SelectMany(mid => lessonsByModule.TryGetValue(mid, out var ls)
                    ? ls
                    : Enumerable.Empty<Guid>())
                .ToHashSet();

            if (trailLessonIds.Count > 0 && trailLessonIds.All(completedLessonIds.Contains))
                count++;
        }

        return count;
    }

    private static int GetCurrentWeekNumber()
    {
        var today = DateTime.UtcNow;
        var week = ISOWeek.GetWeekOfYear(today);
        var year = ISOWeek.GetYear(today);
        return year * 100 + week;
    }

    private static class BadgeCodes
    {
        public const string OnFire             = "on_fire";
        public const string FirstDeploy        = "first_deploy";
        public const string VintageContributor = "vintage_contributor";
        public const string TopDev             = "top_dev";
        public const string KnowledgeSeeker    = "knowledge_seeker";
        public const string SpeedRunner        = "speed_runner";
        public const string QuizMaster         = "quiz_master";
    }
}
