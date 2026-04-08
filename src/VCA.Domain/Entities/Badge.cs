namespace VCA.Domain.Entities;

/// <summary>
/// Conquista (badge) que pode ser obtida pelo usuário ao atingir marcos de gamificação.
/// </summary>
public class Badge
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string? IconUrl { get; private set; }
    public int XpBonus { get; private set; }

    public ICollection<UserBadge> UserBadges { get; private set; } = [];

    private Badge() { }

    public static Badge Create(string code, string name, string description, int xpBonus, string? iconUrl = null)
    {
        return new Badge
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            Description = description,
            XpBonus = xpBonus,
            IconUrl = iconUrl
        };
    }
}
