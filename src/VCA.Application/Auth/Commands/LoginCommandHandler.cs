using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using VCA.Application.Auth.Common;
using VCA.Application.Interfaces;
using VCA.Domain.Common;
using VCA.Domain.Interfaces;

namespace VCA.Application.Auth.Commands;

public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    private readonly ISupabaseAuthService _supabase;
    private readonly IUserSyncService _userSync;
    private readonly IUnitOfWork _uow;
    private readonly IValidator<LoginCommand> _validator;
    private readonly ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(
        ISupabaseAuthService supabase,
        IUserSyncService userSync,
        IUnitOfWork uow,
        IValidator<LoginCommand> validator,
        ILogger<LoginCommandHandler> logger)
    {
        _supabase = supabase;
        _userSync = userSync;
        _uow = uow;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            // Mensagem genérica para não vazar informação a atacante (defesa em profundidade).
            return Result.Failure<LoginResponse>("Credenciais inválidas.");

        SupabaseSession session;
        try
        {
            session = await _supabase.LoginAsync(request.Email, request.Password, cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning("Falha de login para e-mail '{Email}'.", request.Email);
            return Result.Failure<LoginResponse>("Credenciais inválidas.");
        }

        var user = await _userSync.UpsertFromSupabaseAsync(session.User, cancellationToken);

        // Atualizar streak: AddXp(0) já marca LastActivityAt; aqui apenas incrementamos streak
        // se a última atividade foi exatamente no dia anterior. (Fora dessa janela, reset.)
        UpdateStreak(user.LastActivityAt, user);
        await _uow.SaveChangesAsync(cancellationToken);

        var dto = new UserDto(user.Id, user.Email, user.Name, user.AvatarUrl,
            user.Xp, user.Level.ToString(), user.StreakDays);
        var tokens = new TokenResponse(
            session.AccessToken, session.RefreshToken, session.ExpiresIn, session.ExpiresAt);

        return Result.Success(new LoginResponse(tokens, dto));
    }

    private static void UpdateStreak(DateTime? last, Domain.Entities.User user)
    {
        var today = DateTime.UtcNow.Date;
        if (last is null) { user.IncrementStreak(); return; }
        var lastDay = last.Value.Date;
        if (lastDay == today) return;
        if (lastDay == today.AddDays(-1)) user.IncrementStreak();
        else user.ResetStreak();
    }
}
