namespace VCA.Domain.Entities;

/// <summary>
/// Registro de conclusão de uma aula por um usuário.
/// </summary>
public class UserProgress
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid LessonId { get; private set; }
    public DateTime CompletedAt { get; private set; }
    public int XpEarned { get; private set; }

    public User? User { get; private set; }
    public Lesson? Lesson { get; private set; }

    private UserProgress() { }

    public static UserProgress Create(Guid userId, Guid lessonId, int xpEarned)
    {
        return new UserProgress
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            LessonId = lessonId,
            XpEarned = xpEarned,
            CompletedAt = DateTime.UtcNow
        };
    }
}
