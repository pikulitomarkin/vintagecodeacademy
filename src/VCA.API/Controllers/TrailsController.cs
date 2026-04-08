using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VCA.Application.Courses.GetTrails;

namespace VCA.API.Controllers;

/// <summary>
/// Endpoints para listagem de trilhas de aprendizado.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TrailsController : ControllerBase
{
    private readonly GetTrailsHandler _handler;

    public TrailsController(GetTrailsHandler handler) => _handler = handler;

    /// <summary>Lista todas as trilhas publicadas.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _handler.HandleAsync(new GetTrailsQuery(PublishedOnly: true), cancellationToken);
        return Ok(result.Value);
    }
}
