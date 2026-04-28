using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using VCA.Application.Auth.Common;
using VCA.Application.Interfaces;
using VCA.Domain.Common;

namespace VCA.Application.Auth.Commands;

public sealed class RegisterCommandHandler : IRequestHandler<RegisterCommand, Result<UserDto>>
{
    /// <summary>XP de boas-vindas ao primeiro acesso.</summary>
    public const int FirstAccessXp = 10;

    private readonly ISupabaseAuthService _supabase;
    private readonly IUserSyncService _userSync;
    private readonly IEmailService _email;
    private readonly IValidator<RegisterCommand> _validator;
    private readonly ILogger<RegisterCommandHandler> _logger;

    public RegisterCommandHandler(
        ISupabaseAuthService supabase,
        IUserSyncService userSync,
        IEmailService email,
        IValidator<RegisterCommand> validator,
        ILogger<RegisterCommandHandler> logger)
    {
        _supabase = supabase;
        _userSync = userSync;
        _email = email;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<UserDto>> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return Result.Failure<UserDto>(string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));

        SupabaseUser supabaseUser;
        try
        {
            supabaseUser = await _supabase.RegisterAsync(request.Email, request.Password, request.Name, cancellationToken);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already", StringComparison.OrdinalIgnoreCase))
        {
            // Mensagem genérica para evitar enumeração de e-mails (defesa contra account-enumeration).
            return Result.Failure<UserDto>("Não foi possível concluir o registro. Verifique os dados informados.");
        }

        var user = await _userSync.UpsertFromSupabaseAsync(supabaseUser, cancellationToken);
        user.AddXp(FirstAccessXp);

        // Disparo fire-and-forget — falha de e-mail não bloqueia o registro.
        _ = _email.SendWelcomeEmailAsync(user.Email, user.Name, CancellationToken.None);

        _logger.LogInformation("Registro concluído: {UserId} ({Email})", user.Id, user.Email);

        return Result.Success(new UserDto(
            user.Id, user.Email, user.Name, user.AvatarUrl,
            user.Xp, user.Level.ToString(), user.StreakDays));
    }
}
