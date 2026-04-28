namespace VCA.Domain.Common;

/// <summary>
/// Exceção lançada quando uma invariante de domínio é violada.
/// </summary>
public class DomainException : Exception
{
    public string Code { get; }

    public DomainException(string message, string code = "domain.error")
        : base(message)
    {
        Code = code;
    }

    public DomainException(string message, Exception inner, string code = "domain.error")
        : base(message, inner)
    {
        Code = code;
    }
}
