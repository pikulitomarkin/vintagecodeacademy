using VCA.Domain.Interfaces;
using VCA.Infrastructure.Data;

namespace VCA.Infrastructure.Repositories;

/// <summary>
/// Implementação do Unit of Work — centraliza os repositórios e o SaveChanges do EF Core.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public IUserRepository Users { get; }
    public ITrailRepository Trails { get; }
    public IModuleRepository Modules { get; }
    public ILessonRepository Lessons { get; }
    public IQuizRepository Quizzes { get; }
    public IQuizAttemptRepository QuizAttempts { get; }
    public IUserProgressRepository UserProgresses { get; }
    public IBadgeRepository Badges { get; }
    public IRankingRepository Rankings { get; }
    public ILabProjectRepository LabProjects { get; }
    public ILabApplicationRepository LabApplications { get; }
    public IDonationRepository Donations { get; }
    public IAiGenerationLogRepository AiGenerationLogs { get; }

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
        Users = new UserRepository(context);
        Trails = new TrailRepository(context);
        Modules = new ModuleRepository(context);
        Lessons = new LessonRepository(context);
        Quizzes = new QuizRepository(context);
        QuizAttempts = new QuizAttemptRepository(context);
        UserProgresses = new UserProgressRepository(context);
        Badges = new BadgeRepository(context);
        Rankings = new RankingRepository(context);
        LabProjects = new LabProjectRepository(context);
        LabApplications = new LabApplicationRepository(context);
        Donations = new DonationRepository(context);
        AiGenerationLogs = new AiGenerationLogRepository(context);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);

    public void Dispose() => _context.Dispose();
}
