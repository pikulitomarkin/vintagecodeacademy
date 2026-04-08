using Microsoft.EntityFrameworkCore;
using VCA.Domain.Entities;

namespace VCA.Infrastructure.Data;

/// <summary>
/// DbContext principal da aplicação — conectado ao PostgreSQL via Supabase/Npgsql.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Trail> Trails => Set<Trail>();
    public DbSet<Module> Modules => Set<Module>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<LessonChunk> LessonChunks => Set<LessonChunk>();
    public DbSet<Quiz> Quizzes => Set<Quiz>();
    public DbSet<QuizAttempt> QuizAttempts => Set<QuizAttempt>();
    public DbSet<UserProgress> UserProgresses => Set<UserProgress>();
    public DbSet<Badge> Badges => Set<Badge>();
    public DbSet<UserBadge> UserBadges => Set<UserBadge>();
    public DbSet<Ranking> Rankings => Set<Ranking>();
    public DbSet<LabProject> LabProjects => Set<LabProject>();
    public DbSet<LabApplication> LabApplications => Set<LabApplication>();
    public DbSet<Donation> Donations => Set<Donation>();
    public DbSet<AiGenerationLog> AiGenerationLogs => Set<AiGenerationLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
