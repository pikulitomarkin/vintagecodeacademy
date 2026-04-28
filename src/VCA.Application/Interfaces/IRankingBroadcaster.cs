namespace VCA.Application.Interfaces;

/// <summary>
/// Abstração para transmissão em tempo real de atualizações do ranking.
/// Implementada na camada de infraestrutura/API com SignalR.
/// </summary>
public interface IRankingBroadcaster
{
    /// <summary>
    /// Transmite a entrada de ranking atualizada de um usuário para todos os clientes
    /// conectados na semana correspondente.
    /// </summary>
    Task BroadcastAsync(RankingBroadcastEntry entry, CancellationToken cancellationToken = default);
}

/// <summary>Dados transmitidos ao cliente SignalR após atualização de ranking.</summary>
public record RankingBroadcastEntry(
    string Week,
    int Position,
    Guid UserId,
    string UserName,
    string? AvatarUrl,
    int XpEarned,
    string Level);
