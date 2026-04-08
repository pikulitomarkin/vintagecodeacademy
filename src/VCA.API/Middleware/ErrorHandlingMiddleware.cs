using System.Net;
using System.Text.Json;

namespace VCA.API.Middleware;

/// <summary>
/// Middleware global de tratamento de exceções não capturadas.
/// Retorna JSON padronizado com mensagem de erro sem expor detalhes internos em produção.
/// </summary>
public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro não tratado na requisição {Method} {Path}", context.Request.Method, context.Request.Path);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = exception switch
        {
            UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
            KeyNotFoundException => (int)HttpStatusCode.NotFound,
            ArgumentException => (int)HttpStatusCode.BadRequest,
            _ => (int)HttpStatusCode.InternalServerError
        };

        var response = new
        {
            error = context.Response.StatusCode == (int)HttpStatusCode.InternalServerError
                ? "Ocorreu um erro interno. Tente novamente mais tarde."
                : exception.Message
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
