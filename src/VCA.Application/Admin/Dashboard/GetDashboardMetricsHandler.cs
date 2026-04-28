using MediatR;
using VCA.Application.Admin.Common;
using VCA.Domain.Enums;
using VCA.Domain.Interfaces;

namespace VCA.Application.Admin.Dashboard;

public sealed class GetDashboardMetricsHandler : IRequestHandler<GetDashboardMetricsQuery, DashboardMetricsDto>
{
    private readonly IUnitOfWork _uow;

    public GetDashboardMetricsHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<DashboardMetricsDto> Handle(GetDashboardMetricsQuery request, CancellationToken cancellationToken)
    {
        var published = await _uow.Lessons.GetByStatusAsync(LessonStatus.Published, cancellationToken);
        var draft = await _uow.Lessons.GetByStatusAsync(LessonStatus.Draft, cancellationToken);
        var pending = await _uow.Lessons.GetByStatusAsync(LessonStatus.PendingReview, cancellationToken);

        var totalCost = await _uow.AiGenerationLogs.GetTotalCostAsync(cancellationToken);

        // Custo mensal — soma todos os logs do mês corrente.
        var allLogs = await _uow.AiGenerationLogs.GetAllAsync(cancellationToken);
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthlyCost = allLogs
            .Where(l => l.CreatedAt >= monthStart)
            .Sum(l => l.CostUsd);

        // Pontuação média de quiz.
        var allAttempts = await _uow.QuizAttempts.GetAllAsync(cancellationToken);
        var avgScore = allAttempts.Count == 0 ? 0.0 : allAttempts.Average(a => (double)a.Score);

        // Usuários ativos últimos 7 dias = quem teve QuizAttempt ou progresso.
        var weekAgo = DateTime.UtcNow.AddDays(-7);
        var activeUserIds = allAttempts
            .Where(a => a.AttemptedAt >= weekAgo)
            .Select(a => a.UserId)
            .Distinct()
            .Count();

        // Quiz total
        var allQuizzes = await _uow.Quizzes.GetAllAsync(cancellationToken);

        // XP distribuído nos últimos 7 dias (via UserProgress).
        var allProgress = await _uow.UserProgresses.GetAllAsync(cancellationToken);
        var sevenDaysAgo = DateTime.UtcNow.Date.AddDays(-6);
        var xpByDay = Enumerable.Range(0, 7)
            .Select(i => sevenDaysAgo.AddDays(i))
            .Select(day =>
            {
                var dayStart = day;
                var dayEnd = day.AddDays(1);
                var totalXp = allProgress
                    .Where(p => p.CompletedAt >= dayStart && p.CompletedAt < dayEnd)
                    .Sum(p => p.XpEarned);
                return new XpDayPointDto(day, totalXp);
            })
            .ToList();

        return new DashboardMetricsDto(
            PublishedLessons: published.Count,
            DraftLessons: draft.Count,
            PendingReviewLessons: pending.Count,
            TotalQuizQuestions: allQuizzes.Count,
            TotalAiCostUsd: totalCost,
            MonthlyAiCostUsd: monthlyCost,
            AverageQuizScore: avgScore,
            ActiveUsersLastWeek: activeUserIds,
            XpDistributionLast7Days: xpByDay);
    }
}
