using System.Net.Http.Headers;

namespace VCA.Web.Services;

public class ApiAuthorizationMessageHandler : DelegatingHandler
{
    private readonly AuthService _authService;

    public ApiAuthorizationMessageHandler(AuthService authService)
    {
        _authService = authService;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_authService.Token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authService.Token);
        }

        return base.SendAsync(request, cancellationToken);
    }
}