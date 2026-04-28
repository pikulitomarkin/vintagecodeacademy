using MediatR;
using VCA.Domain.Common;

namespace VCA.Application.Gamification.Queries;

/// <summary>
/// Query MediatR para obter o ranking semanal paginado.
/// Week: string no formato "YYYYWW" (ex: "202401"). Se nulo, usa a semana atual.
/// </summary>
public record GetRankingQuery(string? Week, int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<RankingEntryDto>>>;

/// <summary>
/// Entrada do ranking semanal.
/// </summary>
public record RankingEntryDto(
    int Position,
    Guid UserId,
    string UserName,
    string? AvatarUrl,
    int XpEarned);

/// <summary>
/// Resultado paginado genérico.
/// </summary>
public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}
