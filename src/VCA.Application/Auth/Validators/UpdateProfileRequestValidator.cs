using System.Text.RegularExpressions;
using FluentValidation;

namespace VCA.Application.Auth.Validators;

public sealed record UpdateProfileRequest(string Name, string? AvatarUrl);

/// <summary>
/// Validação de atualização de perfil — sanitiza nome (sem HTML) e
/// valida URL do avatar (apenas https + lista de domínios confiáveis).
/// </summary>
public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    private static readonly Regex HtmlTagRegex = new(@"<[^>]+>", RegexOptions.Compiled);

    /// <summary>Lista de domínios autorizados para avatares (defesa contra SSRF/XSS via image source).</summary>
    private static readonly HashSet<string> AllowedAvatarHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "supabase.co",
        "githubusercontent.com",
        "googleusercontent.com",
        "gravatar.com",
        "vintagecodeacademy.com"
    };

    public UpdateProfileRequestValidator()
    {
        RuleFor(r => r.Name)
            .NotEmpty()
            .MinimumLength(2).MaximumLength(80)
            .Must(n => !HtmlTagRegex.IsMatch(n)).WithMessage("Nome não pode conter HTML.")
            .Matches(@"^[^<>""']+$").WithMessage("Nome contém caracteres inválidos.");

        RuleFor(r => r.AvatarUrl)
            .Must(BeValidAvatarUrl).When(r => !string.IsNullOrWhiteSpace(r.AvatarUrl))
            .WithMessage("URL de avatar deve usar HTTPS e domínio autorizado.");
    }

    private static bool BeValidAvatarUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return true;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttps) return false;
        return AllowedAvatarHosts.Any(host =>
            uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase) ||
            uri.Host.EndsWith("." + host, StringComparison.OrdinalIgnoreCase));
    }
}
