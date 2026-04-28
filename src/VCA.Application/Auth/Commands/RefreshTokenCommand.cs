using MediatR;
using VCA.Application.Auth.Common;
using VCA.Application.Interfaces;
using VCA.Domain.Common;

namespace VCA.Application.Auth.Commands;

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<Result<TokenResponse>>;

public sealed class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<TokenResponse>>
{
    private readonly ISupabaseAuthService _supabase;

    public RefreshTokenCommandHandler(ISupabaseAuthService supabase) => _supabase = supabase;

    public async Task<Result<TokenResponse>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return Result.Failure<TokenResponse>("Refresh token ausente.");

        try
        {
            var session = await _supabase.RefreshSessionAsync(request.RefreshToken, cancellationToken);
            return Result.Success(new TokenResponse(
                session.AccessToken, session.RefreshToken, session.ExpiresIn, session.ExpiresAt));
        }
        catch (UnauthorizedAccessException)
        {
            return Result.Failure<TokenResponse>("Refresh token inválido ou expirado.");
        }
    }
}
