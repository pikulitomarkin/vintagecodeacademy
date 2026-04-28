using VCA.Domain.Enums;

namespace VCA.Application.Gamification;

/// <summary>
/// Mapeamento de XP acumulado para o nível (UserLevel) correspondente.
/// </summary>
public static class LevelThresholds
{
    public static readonly IReadOnlyDictionary<UserLevel, int> MinXpByLevel =
        new Dictionary<UserLevel, int>
        {
            [UserLevel.Rookie]     = 0,
            [UserLevel.Apprentice] = 500,
            [UserLevel.Builder]    = 1_500,
            [UserLevel.Craftsman]  = 4_000,
            [UserLevel.Expert]     = 10_000,
            [UserLevel.VintageDev] = 25_000,
        };

    /// <summary>
    /// Retorna o nível correspondente ao XP acumulado.
    /// Espelha a lógica de User.CalculateLevel para uso na camada Application.
    /// </summary>
    public static UserLevel FromXp(int xp) => xp switch
    {
        >= 25_000 => UserLevel.VintageDev,
        >= 10_000 => UserLevel.Expert,
        >= 4_000  => UserLevel.Craftsman,
        >= 1_500  => UserLevel.Builder,
        >= 500    => UserLevel.Apprentice,
        _         => UserLevel.Rookie
    };

    /// <summary>
    /// Retorna o limiar de XP do próximo nível. Retorna int.MaxValue para VintageDev.
    /// </summary>
    public static int NextThreshold(UserLevel level) => level switch
    {
        UserLevel.Rookie      => MinXpByLevel[UserLevel.Apprentice],
        UserLevel.Apprentice  => MinXpByLevel[UserLevel.Builder],
        UserLevel.Builder     => MinXpByLevel[UserLevel.Craftsman],
        UserLevel.Craftsman   => MinXpByLevel[UserLevel.Expert],
        UserLevel.Expert      => MinXpByLevel[UserLevel.VintageDev],
        _                     => int.MaxValue
    };
}
