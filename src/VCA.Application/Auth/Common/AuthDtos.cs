namespace VCA.Application.Auth.Common;

/// <summary>
/// Sessão emitida pelo Supabase Auth após login bem-sucedido.
/// </summary>
public sealed record SupabaseSession(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresIn,
    DateTime ExpiresAt,
    SupabaseUser User);

/// <summary>
/// Representação canônica de um usuário Supabase (campos relevantes para o domínio).
/// </summary>
public sealed record SupabaseUser(
    Guid Id,
    string Email,
    string? Name,
    string? AvatarUrl,
    string? Provider,
    bool EmailConfirmed);

/// <summary>
/// Tokens entregues ao cliente Web. Refresh token deve ser tratado como segredo
/// (idealmente armazenado em cookie HttpOnly + SameSite=Strict).
/// </summary>
public sealed record TokenResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    DateTime ExpiresAt);

/// <summary>
/// Projeção do usuário local para o cliente. Não expõe campos sensíveis.
/// </summary>
public sealed record UserDto(
    Guid Id,
    string Email,
    string Name,
    string? AvatarUrl,
    int Xp,
    string Level,
    int StreakDays);
