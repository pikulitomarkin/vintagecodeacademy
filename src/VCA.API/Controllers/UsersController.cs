using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VCA.Application.Users.RegisterUser;

namespace VCA.API.Controllers;

/// <summary>
/// Endpoints de gerenciamento de usuários.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly RegisterUserHandler _registerHandler;

    public UsersController(RegisterUserHandler registerHandler)
    {
        _registerHandler = registerHandler;
    }

    /// <summary>
    /// Sincroniza o usuário autenticado via Supabase com o banco da aplicação.
    /// Chamado automaticamente após o primeiro login.
    /// </summary>
    [HttpPost("sync")]
    public async Task<IActionResult> Sync([FromBody] SyncUserRequest request, CancellationToken cancellationToken)
    {
        var command = new RegisterUserCommand(request.SupabaseUserId, request.Email, request.Name, request.AvatarUrl);
        var result = await _registerHandler.HandleAsync(command, cancellationToken);

        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        return Ok(new { userId = result.Value });
    }
}

public record SyncUserRequest(Guid SupabaseUserId, string Email, string Name, string? AvatarUrl);
