using FluentValidation;
using MediatR;
using VCA.Application.Auth.Common;
using VCA.Domain.Common;

namespace VCA.Application.Auth.Commands;

public sealed record LoginCommand(string Email, string Password) : IRequest<Result<LoginResponse>>;

public sealed record LoginResponse(TokenResponse Tokens, UserDto User);

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(c => c.Password).NotEmpty().MaximumLength(128);
    }
}
