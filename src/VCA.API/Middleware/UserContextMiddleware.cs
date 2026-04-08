using System.Security.Claims;

namespace VCA.API.Middleware;

/// <summary>
/// Middleware que extrai o userId do JWT e o disponibiliza via HttpContext.Items.
/// Usado pelos controllers para identificar o usuário autenticado.
/// </summary>
public class UserContextMiddleware
{
    private readonly RequestDelegate _next;

    public UserContextMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)
                       ?? context.User.FindFirst("sub");

        if (userIdClaim is not null && Guid.TryParse(userIdClaim.Value, out var userId))
        {
            context.Items["UserId"] = userId;
        }

        await _next(context);
    }
}
