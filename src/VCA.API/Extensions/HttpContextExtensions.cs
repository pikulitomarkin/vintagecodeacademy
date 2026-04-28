namespace VCA.API.Extensions;

/// <summary>
/// Extensões para HttpContext — facilita extração do userId do JWT processado pelo ExtractUserIdMiddleware.
/// </summary>
public static class HttpContextExtensions
{
    /// <summary>
    /// Retorna o UserId extraído do JWT pelo ExtractUserIdMiddleware.
    /// Lança UnauthorizedAccessException se o usuário não estiver autenticado.
    /// </summary>
    public static Guid GetUserId(this HttpContext context)
    {
        if (context.Items["UserId"] is Guid userId)
            return userId;

        throw new UnauthorizedAccessException("Usuário não autenticado.");
    }

    /// <summary>
    /// Retorna o UserId ou null caso o usuário não esteja autenticado.
    /// </summary>
    public static Guid? GetUserIdOrNull(this HttpContext context)
        => context.Items["UserId"] is Guid userId ? userId : null;

    /// <summary>Alias retrocompatível.</summary>
    public static Guid? TryGetUserId(this HttpContext context)
        => context.GetUserIdOrNull();
}
