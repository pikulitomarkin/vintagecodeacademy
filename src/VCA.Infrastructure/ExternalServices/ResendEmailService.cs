using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VCA.Application.Interfaces;

namespace VCA.Infrastructure.ExternalServices;

/// <summary>
/// Envio de e-mails transacionais via API Resend.
/// </summary>
public class ResendEmailService : IEmailService
{
    private readonly HttpClient _httpClient;
    private readonly string _fromEmail;
    private readonly ILogger<ResendEmailService> _logger;

    public ResendEmailService(HttpClient httpClient, IConfiguration config, ILogger<ResendEmailService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        var apiKey = config["Resend:ApiKey"]
            ?? throw new InvalidOperationException("Resend:ApiKey não configurado.");
        _fromEmail = config["Resend:FromEmail"] ?? "noreply@vintagecodeacademy.com";

        _httpClient.BaseAddress = new Uri("https://api.resend.com/");
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
    }

    public async Task SendWelcomeEmailAsync(string toEmail, string userName, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            from = _fromEmail,
            to = toEmail,
            subject = "Bem-vindo ao VintageCodeAcademy!",
            html = $"<h1>Olá, {userName}!</h1><p>Seja bem-vindo ao VintageCodeAcademy. Sua jornada como dev começa agora!</p>"
        };

        await SendAsync(payload, cancellationToken);
        _logger.LogInformation("E-mail de boas-vindas enviado para '{Email}'.", toEmail);
    }

    public async Task SendBadgeEarnedEmailAsync(string toEmail, string userName, string badgeName, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            from = _fromEmail,
            to = toEmail,
            subject = $"Você conquistou o badge: {badgeName}!",
            html = $"<h1>Parabéns, {userName}!</h1><p>Você conquistou o badge <strong>{badgeName}</strong>. Continue evoluindo!</p>"
        };

        await SendAsync(payload, cancellationToken);
        _logger.LogInformation("E-mail de badge '{Badge}' enviado para '{Email}'.", badgeName, toEmail);
    }

    private async Task SendAsync(object payload, CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsJsonAsync("emails", payload, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
