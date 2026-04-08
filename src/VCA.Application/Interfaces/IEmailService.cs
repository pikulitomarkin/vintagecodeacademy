namespace VCA.Application.Interfaces;

/// <summary>
/// Contrato para envio de e-mails transacionais via Resend.
/// </summary>
public interface IEmailService
{
    Task SendWelcomeEmailAsync(string toEmail, string userName, CancellationToken cancellationToken = default);
    Task SendBadgeEarnedEmailAsync(string toEmail, string userName, string badgeName, CancellationToken cancellationToken = default);
}
