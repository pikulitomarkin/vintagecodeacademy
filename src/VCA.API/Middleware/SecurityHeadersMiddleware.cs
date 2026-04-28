using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace VCA.API.Middleware;

/// <summary>
/// Adiciona headers de segurança a todas as respostas HTTP.
/// Conformidade: OWASP Secure Headers Project.
/// </summary>
public static class SecurityHeadersMiddleware
{
    public static IApplicationBuilder UseVcaSecurityHeaders(this IApplicationBuilder app)
    {
        return app.Use(async (ctx, next) =>
        {
            var headers = ctx.Response.Headers;

            // MIME-sniffing → desabilitado (XSS via tipo errado).
            headers["X-Content-Type-Options"] = "nosniff";

            // Clickjacking → bloqueia frames externos.
            headers["X-Frame-Options"] = "DENY";

            // Referrer mínimo — não vaza path para terceiros.
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

            // Restrição de APIs sensíveis do navegador.
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

            // CSP básico para a API (responses majoritariamente JSON; sem scripts inline).
            headers["Content-Security-Policy"] =
                "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";

            // HSTS — somente em HTTPS.
            if (ctx.Request.IsHttps)
                headers["Strict-Transport-Security"] = "max-age=63072000; includeSubDomains; preload";

            await next();
        });
    }
}
