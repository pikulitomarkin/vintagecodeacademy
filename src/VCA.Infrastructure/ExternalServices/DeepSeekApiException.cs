namespace VCA.Infrastructure.ExternalServices;

/// <summary>
/// Exceção lançada quando a API DeepSeek retorna um erro irrecuperável.
/// </summary>
public class DeepSeekApiException : Exception
{
    public int? StatusCode { get; }
    public string? ErrorBody { get; }

    public DeepSeekApiException(string message, int? statusCode = null, string? errorBody = null)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorBody = errorBody;
    }

    public DeepSeekApiException(string message, Exception inner, int? statusCode = null, string? errorBody = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
        ErrorBody = errorBody;
    }
}
