namespace VCA.Application.Gamification;

/// <summary>
/// Constantes de XP concedido por tipo de evento gamificado.
/// </summary>
public static class XpEvents
{
    public const int LessonCompleted         = 10;
    public const int QuickChallengeCompleted = 15;
    public const int QuizPerfectScore        = 30;
    public const int WeekStreak              = 100;
    public const int ProjectDelivered        = 200;
    public const int WeeklyRankingTop3       = 500;
    public const int ForumHelpful            = 20;
    public const int DailyLogin              = 5;

    /// <summary>
    /// Retorna a quantidade de XP correspondente a uma razão de evento.
    /// </summary>
    public static int ForReason(XpReason reason) => reason switch
    {
        XpReason.LessonCompleted         => LessonCompleted,
        XpReason.QuickChallengeCompleted => QuickChallengeCompleted,
        XpReason.QuizPerfectScore        => QuizPerfectScore,
        XpReason.WeekStreak              => WeekStreak,
        XpReason.ProjectDelivered        => ProjectDelivered,
        XpReason.WeeklyRankingTop3       => WeeklyRankingTop3,
        XpReason.ForumHelpful            => ForumHelpful,
        XpReason.DailyLogin              => DailyLogin,
        _                                => 0
    };
}
