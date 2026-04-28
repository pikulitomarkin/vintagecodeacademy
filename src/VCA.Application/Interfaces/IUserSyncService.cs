using VCA.Application.Auth.Common;
using VCA.Domain.Entities;

namespace VCA.Application.Interfaces;

/// <summary>
/// Sincronização entre usuário Supabase e o registro local (upsert por Id).
/// </summary>
public interface IUserSyncService
{
    Task<User> UpsertFromSupabaseAsync(SupabaseUser supabaseUser, CancellationToken cancellationToken = default);
}
