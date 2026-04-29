using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using VCA.Application.Auth.Commands;
using VCA.Application.Auth.Common;
using VCA.IntegrationTests.Infrastructure;

namespace VCA.IntegrationTests.Scenarios;

/// <summary>
/// Fluxo completo de autenticação: register → login → refresh → logout
/// e verificação de rate limiting (5/IP/minuto na rota de login).
/// </summary>
[Collection("Vca")]
public class AuthIntegrationTests
{
    private readonly VcaWebApplicationFactory _factory;
    public AuthIntegrationTests(VcaWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task RegisterLoginRefreshLogout_FullFlow_Succeeds()
    {
        var client = _factory.CreateClient();

        var email = $"user-{Guid.NewGuid():N}@test.com";
        const string password = "Senh@123Forte!";
        var name = "Tester";

        // Register
        var registerResp = await client.PostAsJsonAsync(
            "/api/auth/register", new RegisterCommand(email, password, name));
        registerResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var user = await registerResp.Content.ReadFromJsonAsync<UserDto>();
        user.Should().NotBeNull();
        user!.Email.Should().Be(email);

        // Login
        var loginResp = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginCommand(email, password));
        loginResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var login = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();
        login.Should().NotBeNull();
        login!.Tokens.AccessToken.Should().NotBeNullOrEmpty();
        login.Tokens.RefreshToken.Should().NotBeNullOrEmpty();

        // Refresh
        var refreshResp = await client.PostAsJsonAsync(
            "/api/auth/refresh", new RefreshTokenCommand(login.Tokens.RefreshToken));
        refreshResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var newTokens = await refreshResp.Content.ReadFromJsonAsync<TokenResponse>();
        newTokens!.AccessToken.Should().NotBeNullOrEmpty();
        newTokens.AccessToken.Should().NotBe(login.Tokens.AccessToken);

        // Logout (autenticado via TestAuthHandler)
        var logoutReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logoutReq.Headers.Add(TestAuthHandler.UserIdHeader, user.Id.ToString());
        logoutReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.Tokens.AccessToken);
        var logoutResp = await client.SendAsync(logoutReq);
        logoutResp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Login_RateLimited_BlocksAfterFiveAttempts()
    {
        var client = _factory.CreateClient();

        // Cria usuário válido para garantir que o gargalo é o rate limit, não as credenciais.
        var email = $"rl-{Guid.NewGuid():N}@test.com";
        const string password = "Senh@123Forte!";
        await client.PostAsJsonAsync(
            "/api/auth/register", new RegisterCommand(email, password, "RL"));

        // O bucket pode estar parcialmente consumido por outros testes da mesma coleção,
        // então dispara várias tentativas e exige que ao menos uma seja bloqueada (429).
        var statuses = new List<HttpStatusCode>();
        for (int i = 0; i < 10; i++)
        {
            var resp = await client.PostAsJsonAsync(
                "/api/auth/login", new LoginCommand(email, password));
            statuses.Add(resp.StatusCode);
        }

        statuses.Should().Contain(HttpStatusCode.TooManyRequests,
            "rate limit de 5/minuto deve ativar antes da 10ª tentativa");
    }
}
