using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using VCA.API.Extensions;
using VCA.Domain.Enums;
using VCA.Domain.Interfaces;

namespace VCA.API.Filters;

/// <summary>
/// Action filter que exige que o usuário autenticado tenha pelo menos o nível informado.
/// Uso: [RequireLevel(UserLevel.Expert)] em endpoints do Vintage Labs.
///
/// Segurança:
///   - Só aplicado APÓS autenticação — assume HttpContext.GetUserId() válido.
///   - Cache em memória (TTL 5min) para reduzir round-trip ao banco em cada request.
///   - Cache invalidado de forma natural pelo TTL; ações que mudam nível (AddXp) podem
///     opcionalmente chamar IMemoryCache.Remove(CacheKey(userId)) para refresh imediato.
///   - 403 com mensagem informativa (não é vazamento de dados sensíveis).
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class RequireLevelAttribute : Attribute, IAsyncAuthorizationFilter
{
    public static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    public const string CachePrefix = "vca:user-level:";

    public UserLevel MinimumLevel { get; }

    public RequireLevelAttribute(UserLevel minimumLevel)
    {
        MinimumLevel = minimumLevel;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var http = context.HttpContext;
        var userId = http.GetUserIdOrNull();
        if (userId is null)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var cache = http.RequestServices.GetRequiredService<IMemoryCache>();
        var cacheKey = CachePrefix + userId.Value;

        UserLevel currentLevel;
        if (!cache.TryGetValue(cacheKey, out UserLevel cached))
        {
            var uow = http.RequestServices.GetRequiredService<IUnitOfWork>();
            var user = await uow.Users.GetByIdAsync(userId.Value, http.RequestAborted);
            if (user is null)
            {
                context.Result = new UnauthorizedResult();
                return;
            }
            currentLevel = user.Level;
            cache.Set(cacheKey, currentLevel, CacheTtl);
        }
        else
        {
            currentLevel = cached;
        }

        if ((int)currentLevel < (int)MinimumLevel)
        {
            context.Result = new ObjectResult(new
            {
                error = "forbidden",
                message = $"Nível insuficiente. Você está no nível {currentLevel}, necessário {MinimumLevel}."
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}
