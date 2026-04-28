using System.Security.Claims;

namespace VCA.API.Middleware;

/// <summary>
/// Após validação do JWT (UseAuthentication), extrai o claim 'sub' como Guid e
/// popula HttpContext.Items["UserId"]. Usado pelas extensões HttpContext.GetUserId().
///
/// Segurança:
///   - Só executa após UseAuthentication, garantindo que o claim foi assinado.
///   - Falha silenciosamente para usuários anônimos (endpoints públicos não dependem do UserId).
/// </summary>
public sealed class ExtractUserIdMiddleware
{
    public const string UserIdItemKey = "UserId";

    private readonly RequestDelegate _next;

    public ExtractUserIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var sub = context.User.FindFirst(ClaimTypes.NameIdentifier)
                   ?? context.User.FindFirst("sub");

            if (sub is not null && Guid.TryParse(sub.Value, out var userId))
                context.Items[UserIdItemKey] = userId;
        }

        await _next(context);
    }
}
