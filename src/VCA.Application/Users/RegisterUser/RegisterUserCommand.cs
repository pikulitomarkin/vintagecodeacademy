namespace VCA.Application.Users.RegisterUser;

/// <summary>
/// Comando para registrar um novo usuário após autenticação via Supabase Auth.
/// </summary>
public record RegisterUserCommand(Guid SupabaseUserId, string Email, string Name, string? AvatarUrl);
