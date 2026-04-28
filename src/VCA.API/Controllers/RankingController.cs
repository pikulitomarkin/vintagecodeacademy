using System.Globalization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using VCA.API.Extensions;
using VCA.Application.Gamification.Queries;
using VCA.Domain.Interfaces;

namespace VCA.API.Controllers;

/// <summary>
/// Endpoints do ranking — semanal, mensal, hall of fame e posição do usuário.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RankingController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IUnitOfWork _uow;

    public RankingController(ISender sender, IUnitOfWork uow)
    {
        _sender = sender;
        _uow = uow;
    }

    /// <summary>
    /// Retorna o top 100 do ranking semanal paginado.
    /// O parâmetro week aceita o formato YYYY-Www (ex: 2026-W14); se omitido usa a semana atual.
    /// </summary>
    [HttpGet("weekly")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PagedResult<RankingEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [SwaggerOperation(Summary = "Top 100 do ranking semanal")]
    public async Task<ActionResult<PagedResult<RankingEntryDto>>> GetWeekly(
        [FromQuery] string? week,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var weekKey = ParseIsoWeek(week);
        var result = await _sender.Send(new GetRankingQuery(weekKey, page, pageSize), cancellationToken);

        if (result.IsFailure)
            return BadRequest(new ErrorResponse(result.Error!));

        return Ok(result.Value);
    }

    /// <summary>
    /// Retorna o top 100 mensal (agrega todas as semanas do mês informado).
    /// O parâmetro month aceita o formato YYYY-MM (ex: 2026-03); se omitido usa o mês atual.
    /// </summary>
    [HttpGet("monthly")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MonthlyRankingDto), StatusCodes.Status200OK)]
    [SwaggerOperation(Summary = "Top 100 do ranking mensal agregado")]
    public async Task<ActionResult<MonthlyRankingDto>> GetMonthly(
        [FromQuery] string? month,
        CancellationToken cancellationToken = default)
    {
        var referenceDate = ParseYearMonth(month);
        var weekKeys = GetWeekKeysForMonth(referenceDate);

        // Carrega todas as entradas de ranking das semanas do mês, agrega por usuário
        var allEntries = new List<RankingEntryDto>();

        foreach (var weekKey in weekKeys)
        {
            var result = await _sender.Send(new GetRankingQuery(weekKey, 1, 100), cancellationToken);
            if (result.IsSuccess)
                allEntries.AddRange(result.Value!.Items);
        }

        var aggregated = allEntries
            .GroupBy(e => e.UserId)
            .Select(g => new MonthlyEntryDto(
                g.First().UserName,
                g.First().AvatarUrl,
                g.Sum(e => e.XpEarned)))
            .OrderByDescending(e => e.TotalXp)
            .Select((e, i) => e with { Position = i + 1 })
            .Take(100)
            .ToList();

        return Ok(new MonthlyRankingDto(referenceDate.ToString("yyyy-MM"), aggregated));
    }

    /// <summary>
    /// Retorna o hall of fame — top 10 usuários com maior XP acumulado de todos os tempos.
    /// </summary>
    [HttpGet("hall-of-fame")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<HallOfFameEntryDto>), StatusCodes.Status200OK)]
    [SwaggerOperation(Summary = "Top 10 histórico de XP total")]
    public async Task<ActionResult<List<HallOfFameEntryDto>>> GetHallOfFame(CancellationToken cancellationToken)
    {
        var top10 = await _uow.Users.GetTopByXpAsync(10, cancellationToken);

        var dto = top10
            .OrderByDescending(u => u.Xp)
            .Select((u, i) => new HallOfFameEntryDto(i + 1, u.Id, u.Name, u.AvatarUrl, u.Xp, u.Level.ToString()))
            .ToList();

        return Ok(dto);
    }

    /// <summary>Retorna a posição do usuário autenticado no ranking semanal atual.</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(MyRankingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [SwaggerOperation(Summary = "Posição do usuário no ranking semanal atual")]
    public async Task<ActionResult<MyRankingDto>> GetMyRanking(CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetUserId();
        var currentWeek = GetCurrentWeekKey();

        var weekNum = int.Parse(currentWeek);
        var entry = await _uow.Rankings.GetByUserAndWeekAsync(userId, weekNum, cancellationToken);

        if (entry is null)
            return Ok(new MyRankingDto(userId, 0, 0, currentWeek));

        return Ok(new MyRankingDto(userId, entry.Position, entry.XpEarned, currentWeek));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string? ParseIsoWeek(string? week)
    {
        if (string.IsNullOrWhiteSpace(week)) return null;

        // Aceita "2026-W14" e converte para "202614"
        if (week.Contains("-W", StringComparison.OrdinalIgnoreCase))
        {
            var parts = week.Split("-W", StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && int.TryParse(parts[0], out var y) && int.TryParse(parts[1], out var w))
                return $"{y * 100 + w}";
        }

        return week;
    }

    private static DateTime ParseYearMonth(string? month)
    {
        if (string.IsNullOrWhiteSpace(month))
            return DateTime.UtcNow;

        return DateTime.TryParseExact(month, "yyyy-MM",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : DateTime.UtcNow;
    }

    private static List<string> GetWeekKeysForMonth(DateTime referenceDate)
    {
        var keys = new List<string>();
        var firstDay = new DateTime(referenceDate.Year, referenceDate.Month, 1);
        var lastDay = firstDay.AddMonths(1).AddDays(-1);

        var current = firstDay;
        while (current <= lastDay)
        {
            var week = ISOWeek.GetWeekOfYear(current);
            var year = ISOWeek.GetYear(current);
            var key = $"{year * 100 + week}";
            if (!keys.Contains(key)) keys.Add(key);
            current = current.AddDays(7);
        }

        return keys;
    }

    private static string GetCurrentWeekKey()
    {
        var today = DateTime.UtcNow;
        var week = ISOWeek.GetWeekOfYear(today);
        var year = ISOWeek.GetYear(today);
        return $"{year * 100 + week}";
    }
}

public record MonthlyRankingDto(string Month, List<MonthlyEntryDto> Entries);
public record MonthlyEntryDto(string UserName, string? AvatarUrl, int TotalXp)
{
    public int Position { get; init; }
}
public record HallOfFameEntryDto(int Position, Guid UserId, string UserName, string? AvatarUrl, int TotalXp, string Level);
public record MyRankingDto(Guid UserId, int Position, int XpEarnedThisWeek, string Week);
