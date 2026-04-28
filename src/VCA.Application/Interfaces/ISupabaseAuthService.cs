using VCA.Application.Auth.Common;

namespace VCA.Application.Interfaces;

/// <summary>
/// Contrato para integração com o Supabase Auth. Encapsula REST/SDK e centraliza
/// validação remota de tokens (defesa em profundidade contra tokens revogados).
/// </summary>
public interface ISupabaseAuthService
{
    /// <summary>Cria conta no Supabase. O usuário precisa confirmar e-mail antes de logar.</summary>
    Task<SupabaseUser> RegisterAsync(string email, string password, string name, CancellationToken cancellationToken = default);

    /// <summary>Login email+senha. Lança UnauthorizedAccessException em credenciais inválidas.</summary>
    Task<SupabaseSession> LoginAsync(string email, string password, CancellationToken cancellationToken = default);

    /// <summary>Retorna URL de redirect para iniciar o fluxo OAuth Google.</summary>
    string LoginWithGoogleUrl(string redirectTo);

    /// <summary>Retorna URL de redirect para iniciar o fluxo OAuth GitHub.</summary>
    string LoginWithGitHubUrl(string redirectTo);

    /// <summary>Renova a sessão usando o refresh token. Lança UnauthorizedAccessException se inválido.</summary>
    Task<SupabaseSession> RefreshSessionAsync(string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>Revoga a sessão no Supabase (best-effort).</summary>
    Task SignOutAsync(string accessToken, CancellationToken cancellationToken = default);

    /// <summary>Valida o access token chamando GET /auth/v1/user. Retorna null se inválido/expirado.</summary>
    Task<SupabaseUser?> GetUserFromTokenAsync(string accessToken, CancellationToken cancellationToken = default);
}
