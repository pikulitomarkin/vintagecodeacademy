namespace VCA.Web.Services;

/// <summary>
/// Serviço de autenticação — gerencia o token JWT do Supabase Auth no Blazor WASM.
/// </summary>
public class AuthService
{
    private string? _token;

    public event Action? AuthenticationStateChanged;

    public bool IsAuthenticated => !string.IsNullOrEmpty(_token);
    public string? Token => _token;

    public void SetToken(string token)
    {
        _token = token;
        AuthenticationStateChanged?.Invoke();
    }

    public void ClearToken()
    {
        _token = null;
        AuthenticationStateChanged?.Invoke();
    }
}
