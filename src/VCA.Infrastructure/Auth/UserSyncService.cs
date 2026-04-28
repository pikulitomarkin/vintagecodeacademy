using Microsoft.Extensions.Logging;
using VCA.Application.Auth.Common;
using VCA.Application.Interfaces;
using VCA.Domain.Entities;
using VCA.Domain.Interfaces;

namespace VCA.Infrastructure.Auth;

/// <summary>
/// Sincronização entre o usuário Supabase e o registro local. O User.Id local é
/// alinhado ao Supabase Auth ID (mesmo Guid) — eliminando coluna extra supabase_id
/// e mantendo os dois sistemas referencialmente consistentes.
/// </summary>
public sealed class UserSyncService : IUserSyncService
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<UserSyncService> _logger;

    public UserSyncService(IUnitOfWork uow, ILogger<UserSyncService> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<User> UpsertFromSupabaseAsync(SupabaseUser supabaseUser, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(supabaseUser);

        var existing = await _uow.Users.GetByIdAsync(supabaseUser.Id, cancellationToken);
        var displayName = !string.IsNullOrWhiteSpace(supabaseUser.Name)
            ? supabaseUser.Name!
            : DefaultNameFromEmail(supabaseUser.Email);

        if (existing is null)
        {
            var user = User.Create(supabaseUser.Id, supabaseUser.Email, displayName, supabaseUser.AvatarUrl);
            await _uow.Users.AddAsync(user, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Usuário criado via sync: {Id} ({Email})", user.Id, user.Email);
            return user;
        }

        // Atualiza apenas se diferente — minimiza writes.
        if (!string.Equals(existing.Name, displayName, StringComparison.Ordinal)
            || !string.Equals(existing.AvatarUrl, supabaseUser.AvatarUrl, StringComparison.Ordinal))
        {
            existing.UpdateProfile(displayName, supabaseUser.AvatarUrl);
            await _uow.SaveChangesAsync(cancellationToken);
        }
        return existing;
    }

    private static string DefaultNameFromEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return "Vintage Dev";
        var at = email.IndexOf('@');
        return at > 0 ? email[..at] : email;
    }
}
