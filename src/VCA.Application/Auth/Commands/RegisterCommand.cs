using FluentValidation;
using MediatR;
using VCA.Application.Auth.Common;
using VCA.Domain.Common;

namespace VCA.Application.Auth.Commands;

/// <summary>
/// Comando de registro: cria usuário no Supabase + sincroniza local + e-mail de boas-vindas.
/// </summary>
public sealed record RegisterCommand(string Email, string Password, string Name)
    : IRequest<Result<UserDto>>;

/// <summary>
/// Validação de senha forte: mínimo 8 caracteres, ao menos 1 dígito e 1 caractere especial.
/// E-mail validado por sintaxe RFC; nome sanitizado contra HTML básico.
/// </summary>
public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(c => c.Email)
            .NotEmpty().WithMessage("E-mail é obrigatório.")
            .EmailAddress().WithMessage("E-mail inválido.")
            .MaximumLength(254);

        RuleFor(c => c.Password)
            .NotEmpty().WithMessage("Senha é obrigatória.")
            .MinimumLength(8).WithMessage("Senha deve ter no mínimo 8 caracteres.")
            .MaximumLength(128)
            .Matches(@"\d").WithMessage("Senha deve conter ao menos um número.")
            .Matches(@"[^A-Za-z0-9]").WithMessage("Senha deve conter ao menos um caractere especial.");

        RuleFor(c => c.Name)
            .NotEmpty().WithMessage("Nome é obrigatório.")
            .MinimumLength(2)
            .MaximumLength(80)
            .Matches(@"^[^<>]*$").WithMessage("Nome contém caracteres inválidos.");
    }
}
