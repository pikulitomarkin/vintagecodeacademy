using Microsoft.Extensions.Logging;
using VCA.Application.Interfaces;
using VCA.Domain.Common;
using VCA.Domain.Entities;
using VCA.Domain.Interfaces;

namespace VCA.Application.Users.RegisterUser;

/// <summary>
/// Handler para sincronizar o usuário autenticado pelo Supabase Auth com o banco da aplicação.
/// </summary>
public class RegisterUserHandler
{
    private readonly IUnitOfWork _uow;
    private readonly IEmailService _emailService;
    private readonly ILogger<RegisterUserHandler> _logger;

    public RegisterUserHandler(IUnitOfWork uow, IEmailService emailService, ILogger<RegisterUserHandler> logger)
    {
        _uow = uow;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<Result<Guid>> HandleAsync(RegisterUserCommand command, CancellationToken cancellationToken = default)
    {
        var existing = await _uow.Users.GetByEmailAsync(command.Email, cancellationToken);
        if (existing is not null)
            return Result.Success(existing.Id);

        var user = User.Create(command.SupabaseUserId, command.Email, command.Name, command.AvatarUrl);
        await _uow.Users.AddAsync(user, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Novo usuário registrado: '{Email}' (Id: {Id})", user.Email, user.Id);

        // Disparar e-mail de boas-vindas de forma assíncrona
        _ = _emailService.SendWelcomeEmailAsync(user.Email, user.Name, cancellationToken);

        return Result.Success(user.Id);
    }
}
