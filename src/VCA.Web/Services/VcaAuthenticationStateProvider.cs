using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;

namespace VCA.Web.Services;

public class VcaAuthenticationStateProvider : AuthenticationStateProvider
{
    private static readonly AuthenticationState Anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));
    private readonly AuthService _authService;

    public VcaAuthenticationStateProvider(AuthService authService)
    {
        _authService = authService;
        _authService.AuthenticationStateChanged += OnAuthenticationChanged;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (string.IsNullOrWhiteSpace(_authService.Token))
        {
            return Task.FromResult(Anonymous);
        }

        var claims = ParseClaims(_authService.Token);
        var identity = new ClaimsIdentity(claims, authenticationType: "jwt");
        return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
    }

    private void OnAuthenticationChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    private static IEnumerable<Claim> ParseClaims(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length < 2)
        {
            return Array.Empty<Claim>();
        }

        var payload = parts[1];
        var jsonBytes = ParseBase64WithoutPadding(payload);
        var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonBytes) ?? [];
        var claims = new List<Claim>();

        foreach (var pair in keyValuePairs)
        {
            if (pair.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in pair.Value.EnumerateArray())
                {
                    claims.Add(new Claim(pair.Key, item.ToString()));
                }

                continue;
            }

            claims.Add(new Claim(pair.Key, ConvertJsonElementToString(pair.Value)));
        }

        return claims;
    }

    private static byte[] ParseBase64WithoutPadding(string payload)
    {
        payload = payload.Replace('-', '+').Replace('_', '/');

        var padding = 4 - payload.Length % 4;
        if (padding is > 0 and < 4)
        {
            payload = payload.PadRight(payload.Length + padding, '=');
        }

        return Convert.FromBase64String(payload);
    }

    private static string ConvertJsonElementToString(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString() ?? string.Empty,
        JsonValueKind.Number => element.GetRawText(),
        JsonValueKind.True => bool.TrueString,
        JsonValueKind.False => bool.FalseString,
        JsonValueKind.Null => string.Empty,
        _ => element.ToString()
    };
}