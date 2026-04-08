namespace VCA.Domain.Enums;

/// <summary>
/// Níveis de progressão do usuário com base em XP acumulado.
/// </summary>
public enum UserLevel
{
    Rookie = 0,        // 0 XP
    Apprentice = 1,    // 500 XP
    Builder = 2,       // 1.500 XP
    Craftsman = 3,     // 4.000 XP
    Expert = 4,        // 10.000 XP
    VintageDev = 5     // 25.000 XP
}
