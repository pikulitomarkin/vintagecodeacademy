namespace VCA.Domain.Entities;

/// <summary>
/// Relacionamento entre usuário e badge conquistada.
/// </summary>
public class UserBadge
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid BadgeId { get; private set; }
    public DateTime EarnedAt { get; private set; }

    public User? User { get; private set; }
    public Badge? Badge { get; private set; }

    private UserBadge() { }

    public static UserBadge Create(Guid userId, Guid badgeId)
    {
        return new UserBadge
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            BadgeId = badgeId,
            EarnedAt = DateTime.UtcNow
        };
    }
}
