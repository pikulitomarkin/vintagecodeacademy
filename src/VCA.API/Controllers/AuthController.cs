using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;
using VCA.Application.Auth.Commands;
using VCA.Application.Auth.Common;
using VCA.Application.Interfaces;
using VCA.Application.Users.RegisterUser;

namespace VCA.API.Controllers;

/// <summary>
/// Endpoints de autenticação. Backend é fonte de verdade — valida JWT do Supabase
/// e mantém usuário sincronizado. Cliente pode usar SDK Supabase OU estes endpoints.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ISender _mediator;
    private readonly ISupabaseAuthService _supabase;
    private readonly RegisterUserHandler _legacyRegister;

    public AuthController(ISender mediator, ISupabaseAuthService supabase, RegisterUserHandler legacyRegister)
    {
        _mediator = mediator;
        _supabase = supabase;
        _legacyRegister = legacyRegister;
    }

    /// <summary>Registra usuário (email+senha). Rate-limited contra spam (3 / IP / hora).</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-register")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [SwaggerOperation(Summary = "Registra novo usuário no Supabase + sincroniza local")]
    public async Task<IActionResult> Register([FromBody] RegisterCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        if (result.IsFailure) return BadRequest(new ErrorResponse(result.Error!));
        return CreatedAtAction(nameof(Register), result.Value);
    }

    /// <summary>Sincroniza usuário pós-OAuth (frontend chama após Supabase signin com Google/GitHub).</summary>
    [HttpPost("sync")]
    [Authorize]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status200OK)]
    [SwaggerOperation(Summary = "Sincroniza usuário autenticado via OAuth com banco local")]
    public async Task<IActionResult> Sync([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var command = new RegisterUserCommand(request.SupabaseUserId, request.Email, request.Name, request.AvatarUrl);
        var result = await _legacyRegister.HandleAsync(command, cancellationToken);
        if (result.IsFailure) return BadRequest(new ErrorResponse(result.Error!));
        return Ok(new RegisterResponse(result.Value!));
    }

    /// <summary>Login email+senha. Rate-limited a 5 tentativas / IP / minuto contra brute-force.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [SwaggerOperation(Summary = "Login email+senha via Supabase Auth")]
    public async Task<IActionResult> Login([FromBody] LoginCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        if (result.IsFailure)
            return Unauthorized(new ErrorResponse(result.Error!));
        return Ok(result.Value);
    }

    /// <summary>Renova access token usando refresh token.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [SwaggerOperation(Summary = "Renova access token via refresh token")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        if (result.IsFailure)
            return Unauthorized(new ErrorResponse(result.Error!));
        return Ok(result.Value);
    }

    /// <summary>OAuth Google — redireciona para o Supabase. Domínio de retorno em allowlist.</summary>
    [HttpGet("oauth/google")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [SwaggerOperation(Summary = "Inicia fluxo OAuth Google")]
    public IActionResult OAuthGoogle([FromQuery] string redirectTo)
    {
        if (!IsAllowedRedirect(redirectTo)) return BadRequest(new ErrorResponse("redirectTo não autorizado."));
        return Redirect(_supabase.LoginWithGoogleUrl(redirectTo));
    }

    /// <summary>OAuth GitHub — redireciona para o Supabase. Domínio de retorno em allowlist.</summary>
    [HttpGet("oauth/github")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [SwaggerOperation(Summary = "Inicia fluxo OAuth GitHub")]
    public IActionResult OAuthGitHub([FromQuery] string redirectTo)
    {
        if (!IsAllowedRedirect(redirectTo)) return BadRequest(new ErrorResponse("redirectTo não autorizado."));
        return Redirect(_supabase.LoginWithGitHubUrl(redirectTo));
    }

    /// <summary>Logout — revoga sessão no Supabase.</summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [SwaggerOperation(Summary = "Logout — revoga sessão no Supabase")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var header = Request.Headers.Authorization.ToString();
        if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            await _supabase.SignOutAsync(header["Bearer ".Length..].Trim(), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Allowlist de redirect_to para OAuth — defesa contra open-redirect.
    /// Origens autorizadas vêm da configuração (Cors:AllowedOrigins).
    /// </summary>
    private bool IsAllowedRedirect(string? redirectTo)
    {
        if (string.IsNullOrWhiteSpace(redirectTo)) return false;
        if (!Uri.TryCreate(redirectTo, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttps && !(uri.Host == "localhost" || uri.Host == "127.0.0.1")) return false;

        var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var allowed = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
        return allowed.Any(o =>
            Uri.TryCreate(o, UriKind.Absolute, out var u) &&
            string.Equals(u.Host, uri.Host, StringComparison.OrdinalIgnoreCase));
    }
}

public record RegisterRequest(Guid SupabaseUserId, string Email, string Name, string? AvatarUrl);
public record RegisterResponse(Guid UserId);
public record LoginInfoResponse(string Message, string DocsUrl);
public record ErrorResponse(string Error);
