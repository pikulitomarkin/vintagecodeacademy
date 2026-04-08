using VCA.Domain.Enums;

namespace VCA.Domain.Entities;

/// <summary>
/// Aula gamificada gerada por IA ou criada manualmente.
/// Estrutura: Missão | Contexto Real | Conceito | Desafio Rápido | Exemplo | Quiz | Resumo + XP.
/// </summary>
public class Lesson
{
    public Guid Id { get; private set; }
    public Guid ModuleId { get; private set; }
    public string Title { get; private set; } = string.Empty;

    /// <summary>JSON com a estrutura gamificada da aula (gerado pela IA ou manualmente).</summary>
    public string ContentJson { get; private set; } = "{}";

    public int XpReward { get; private set; }
    public int Order { get; private set; }
    public LessonStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? PublishedAt { get; private set; }

    public Module? Module { get; private set; }
    public ICollection<LessonChunk> Chunks { get; private set; } = [];
    public ICollection<Quiz> Quizzes { get; private set; } = [];
    public ICollection<UserProgress> UserProgresses { get; private set; } = [];
    public ICollection<AiGenerationLog> AiLogs { get; private set; } = [];

    private Lesson() { }

    public static Lesson Create(Guid moduleId, string title, int order, int xpReward = 10)
    {
        return new Lesson
        {
            Id = Guid.NewGuid(),
            ModuleId = moduleId,
            Title = title,
            Order = order,
            XpReward = xpReward,
            Status = LessonStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void SetContent(string contentJson) => ContentJson = contentJson;

    public void SubmitForReview() => Status = LessonStatus.PendingReview;

    public void Publish()
    {
        Status = LessonStatus.Published;
        PublishedAt = DateTime.UtcNow;
    }

    public void Archive() => Status = LessonStatus.Archived;

    public void Update(string title, int order, int xpReward)
    {
        Title = title;
        Order = order;
        XpReward = xpReward;
    }
}
