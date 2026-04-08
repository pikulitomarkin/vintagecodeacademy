using VCA.Domain.Enums;

namespace VCA.Domain.Entities;

/// <summary>
/// Representa um usuário registrado na plataforma VintageCodeAcademy.
/// </summary>
public class User
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? AvatarUrl { get; private set; }
    public int Xp { get; private set; }
    public UserLevel Level { get; private set; }
    public int StreakDays { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastActivityAt { get; private set; }

    // Navegação
    public ICollection<UserProgress> Progress { get; private set; } = [];
    public ICollection<UserBadge> Badges { get; private set; } = [];
    public ICollection<QuizAttempt> QuizAttempts { get; private set; } = [];
    public ICollection<LabApplication> LabApplications { get; private set; } = [];
    public ICollection<Donation> Donations { get; private set; } = [];
    public ICollection<Ranking> Rankings { get; private set; } = [];

    private User() { }

    public static User Create(Guid id, string email, string name, string? avatarUrl = null)
    {
        return new User
        {
            Id = id,
            Email = email,
            Name = name,
            AvatarUrl = avatarUrl,
            Xp = 0,
            Level = UserLevel.Rookie,
            StreakDays = 0,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Adiciona XP ao usuário e recalcula o nível.
    /// </summary>
    public void AddXp(int amount)
    {
        if (amount <= 0) return;
        Xp += amount;
        Level = CalculateLevel(Xp);
        LastActivityAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Incrementa o streak diário do usuário.
    /// </summary>
    public void IncrementStreak() => StreakDays++;

    /// <summary>
    /// Zera o streak caso o usuário não tenha interagido no dia anterior.
    /// </summary>
    public void ResetStreak() => StreakDays = 0;

    public void UpdateProfile(string name, string? avatarUrl)
    {
        Name = name;
        AvatarUrl = avatarUrl;
    }

    private static UserLevel CalculateLevel(int xp) => xp switch
    {
        >= 25000 => UserLevel.VintageDev,
        >= 10000 => UserLevel.Expert,
        >= 4000  => UserLevel.Craftsman,
        >= 1500  => UserLevel.Builder,
        >= 500   => UserLevel.Apprentice,
        _        => UserLevel.Rookie
    };
}
