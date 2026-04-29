using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace VCA.IntegrationTests.Infrastructure;

/// <summary>
/// Esquema de autenticação para testes — lê userId/role de headers.
/// </summary>
///   X-Test-UserId: {guid}      → claim sub/NameIdentifier
///   X-Test-Role:   Admin|User  → claim role (default: User)
/// Se nenhum header estiver presente, a requisição é tratada como anônima.
public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";
    public const string UserIdHeader = "X-Test-UserId";
    public const string RoleHeader = "X-Test-Role";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock) : base(options, logger, encoder, clock) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var idHeader = Request.Headers[UserIdHeader].ToString();
        if (string.IsNullOrWhiteSpace(idHeader))
            return Task.FromResult(AuthenticateResult.NoResult());

        if (!Guid.TryParse(idHeader, out var userId))
            return Task.FromResult(AuthenticateResult.Fail("Invalid X-Test-UserId."));

        var role = Request.Headers[RoleHeader].ToString();
        if (string.IsNullOrWhiteSpace(role)) role = "User";

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("sub", userId.ToString()),
            new Claim(ClaimTypes.Role, role),
            new Claim(ClaimTypes.Name, $"test-user-{userId:N}")
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
