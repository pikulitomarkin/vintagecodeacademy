namespace VCA.Domain.Interfaces;

/// <summary>
/// Unidade de trabalho — agrupa repositórios e controla o ciclo de vida da transação.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    IUserRepository Users { get; }
    ITrailRepository Trails { get; }
    IModuleRepository Modules { get; }
    ILessonRepository Lessons { get; }
    IQuizRepository Quizzes { get; }
    IQuizAttemptRepository QuizAttempts { get; }
    IUserProgressRepository UserProgresses { get; }
    IBadgeRepository Badges { get; }
    IRankingRepository Rankings { get; }
    ILabProjectRepository LabProjects { get; }
    ILabApplicationRepository LabApplications { get; }
    IDonationRepository Donations { get; }
    IAiGenerationLogRepository AiGenerationLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
