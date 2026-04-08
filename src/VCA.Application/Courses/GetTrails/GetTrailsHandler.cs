using VCA.Domain.Common;
using VCA.Domain.Entities;
using VCA.Domain.Interfaces;

namespace VCA.Application.Courses.GetTrails;

/// <summary>
/// Handler para listagem de trilhas.
/// </summary>
public class GetTrailsHandler
{
    private readonly IUnitOfWork _uow;

    public GetTrailsHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Result<IReadOnlyList<Trail>>> HandleAsync(GetTrailsQuery query, CancellationToken cancellationToken = default)
    {
        var trails = query.PublishedOnly
            ? await _uow.Trails.GetPublishedAsync(cancellationToken)
            : await _uow.Trails.GetAllAsync(cancellationToken);

        return Result.Success(trails);
    }
}
