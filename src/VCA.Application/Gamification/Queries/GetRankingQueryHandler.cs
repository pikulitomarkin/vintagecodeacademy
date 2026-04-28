using System.Globalization;
using MediatR;
using VCA.Domain.Common;
using VCA.Domain.Interfaces;

namespace VCA.Application.Gamification.Queries;

/// <summary>
/// Handler MediatR para GetRankingQuery.
/// Retorna os top 100 da semana e aplica paginação em memória.
/// </summary>
public class GetRankingQueryHandler
    : IRequestHandler<GetRankingQuery, Result<PagedResult<RankingEntryDto>>>
{
    private const int MaxRankingEntries = 100;

    private readonly IUnitOfWork _uow;

    public GetRankingQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Result<PagedResult<RankingEntryDto>>> Handle(
        GetRankingQuery request, CancellationToken cancellationToken)
    {
        if (request.Page < 1 || request.PageSize < 1)
            return Result.Failure<PagedResult<RankingEntryDto>>("Parâmetros de paginação inválidos.");

        var week = ResolveWeek(request.Week);

        var top100 = await _uow.Rankings.GetWeeklyTopAsync(week, MaxRankingEntries, cancellationToken);

        if (top100.Count == 0)
            return Result.Success(new PagedResult<RankingEntryDto>([], 0, request.Page, request.PageSize));

        // Carrega usuários para montar o DTO com nomes e avatares
        var userIds = top100.Select(r => r.UserId).ToHashSet();
        var users = await _uow.Users.FindAsync(u => userIds.Contains(u.Id), cancellationToken);
        var userById = users.ToDictionary(u => u.Id);

        var totalCount = top100.Count;

        var items = top100
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(r =>
            {
                userById.TryGetValue(r.UserId, out var u);
                return new RankingEntryDto(
                    r.Position,
                    r.UserId,
                    u?.Name ?? "—",
                    u?.AvatarUrl,
                    r.XpEarned);
            })
            .ToList();

        return Result.Success(new PagedResult<RankingEntryDto>(items, totalCount, request.Page, request.PageSize));
    }

    private static int ResolveWeek(string? week)
    {
        if (!string.IsNullOrWhiteSpace(week) && int.TryParse(week, out var parsed))
            return parsed;

        var today = DateTime.UtcNow;
        var weekNum = ISOWeek.GetWeekOfYear(today);
        var year = ISOWeek.GetYear(today);
        return year * 100 + weekNum;
    }
}
