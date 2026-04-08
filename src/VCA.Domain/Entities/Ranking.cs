namespace VCA.Domain.Entities;

/// <summary>
/// Posição de um usuário no ranking semanal de XP.
/// </summary>
public class Ranking
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }

    /// <summary>Número ISO da semana do ano (ex: 202401).</summary>
    public int Week { get; private set; }

    public int XpEarned { get; private set; }
    public int Position { get; private set; }

    public User? User { get; private set; }

    private Ranking() { }

    public static Ranking Create(Guid userId, int week, int xpEarned, int position)
    {
        return new Ranking
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Week = week,
            XpEarned = xpEarned,
            Position = position
        };
    }

    public void UpdateXp(int xpEarned) => XpEarned = xpEarned;
    public void UpdatePosition(int position) => Position = position;
}
