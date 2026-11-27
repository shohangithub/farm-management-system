using Farm360.Application.Common.Interfaces;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Analytics.Queries;

// ══════════════════════════════════════════════════════════════════════════════
// HEALTH TRENDS (12 months) — the Health "Trends" page
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>One calendar month of the trend window. Months with no records are present with zeros.</summary>
public sealed record HealthTrendMonthDto(int Year, int Month, int Deaths, int Treatments, decimal TreatmentCostBdt);

/// <summary>A diagnosis and how many treatments were logged for it in the window.</summary>
public sealed record HealthIllnessCountDto(string Name, int Count);

/// <summary>
/// 12-month health aggregates for one farm, counted over every record in the window
/// (not a page of the latest rows). <see cref="Months"/> runs oldest → newest, current month last.
/// Vaccination coverage = on-farm animals with a vaccination given in the last 6 months.
/// </summary>
public sealed record HealthTrendsDto(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<HealthTrendMonthDto> Months,
    IReadOnlyList<HealthIllnessCountDto> TopIllnesses,
    int TotalDeaths,
    int TotalTreatments,
    decimal TotalTreatmentCostBdt,
    int OnFarmAnimals,
    int VaccinatedAnimals,
    double VaccinationCoveragePercent);

/// <summary>Count (and money total) for one calendar month, as grouped by the database.</summary>
public sealed record HealthMonthAggregate(int Year, int Month, int Count, decimal AmountBdt = 0m);

/// <summary>Grouped figures read in a few set-based queries; the handler shapes them into the window.</summary>
public sealed record HealthTrendSourceData(
    IReadOnlyList<HealthMonthAggregate> DeathsByMonth,
    IReadOnlyList<HealthMonthAggregate> TreatmentsByMonth,
    IReadOnlyList<HealthIllnessCountDto> Illnesses,
    int OnFarmAnimals,
    int VaccinatedAnimals);

/// <summary>Database side of the health trends. Implemented in Persistence.</summary>
public interface IHealthTrendSource
{
    /// <param name="from">First day counted (inclusive).</param>
    /// <param name="to">Last day counted (inclusive).</param>
    /// <param name="vaccinatedSince">Vaccinations given on or after this date count toward coverage.</param>
    /// <param name="topIllnesses">How many diagnoses to return, most frequent first.</param>
    Task<HealthTrendSourceData> GetAsync(
        Guid farmId, DateOnly from, DateOnly to, DateOnly vaccinatedSince, int topIllnesses,
        CancellationToken cancellationToken = default);
}

/// <summary>GET /api/v1/farms/{farmId}/analytics/health/trends. Permission: analytics.view.</summary>
public sealed record GetHealthTrendsQuery(Guid FarmId) : IRequest<HealthTrendsDto>;

public sealed class GetHealthTrendsQueryValidator : AbstractValidator<GetHealthTrendsQuery>
{
    public GetHealthTrendsQueryValidator()
    {
        RuleFor(q => q.FarmId).NotEmpty();
    }
}

public sealed class GetHealthTrendsQueryHandler(IHealthTrendSource source, IFarmClock clock)
    : IRequestHandler<GetHealthTrendsQuery, HealthTrendsDto>
{
    /// <summary>Months in the window, current month included.</summary>
    public const int WindowMonths = 12;

    /// <summary>Diagnoses listed under "Most common illnesses".</summary>
    public const int TopIllnessCount = 5;

    /// <summary>How far back a vaccination still counts toward coverage.</summary>
    public const int CoverageMonths = 6;

    public async Task<HealthTrendsDto> Handle(GetHealthTrendsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var today = clock.Today;
        var currentMonth = new DateOnly(today.Year, today.Month, 1);
        var from = currentMonth.AddMonths(-(WindowMonths - 1));

        var data = await source.GetAsync(
            request.FarmId, from, today, today.AddMonths(-CoverageMonths), TopIllnessCount, cancellationToken)
            .ConfigureAwait(false);

        return Build(data, from, today);
    }

    /// <summary>Zero-fills the 12 months, totals them and ranks the illnesses. Pure, for tests.</summary>
    public static HealthTrendsDto Build(HealthTrendSourceData data, DateOnly from, DateOnly to)
    {
        ArgumentNullException.ThrowIfNull(data);

        var deaths = Index(data.DeathsByMonth);
        var treatments = Index(data.TreatmentsByMonth);

        var months = new List<HealthTrendMonthDto>(WindowMonths);
        for (var i = 0; i < WindowMonths; i++)
        {
            var m = from.AddMonths(i);
            var key = (m.Year, m.Month);
            var d = deaths.GetValueOrDefault(key);
            var t = treatments.GetValueOrDefault(key);
            months.Add(new HealthTrendMonthDto(m.Year, m.Month, d?.Count ?? 0, t?.Count ?? 0, t?.AmountBdt ?? 0m));
        }

        var illnesses = data.Illnesses
            .Where(x => !string.IsNullOrWhiteSpace(x.Name) && x.Count > 0)
            .Select(x => x with { Name = x.Name.Trim() })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Take(TopIllnessCount)
            .ToList();

        var coverage = data.OnFarmAnimals <= 0
            ? 0d
            : Math.Round(Math.Min(data.VaccinatedAnimals, data.OnFarmAnimals) * 100d / data.OnFarmAnimals, 1);

        return new HealthTrendsDto(
            from,
            to,
            months,
            illnesses,
            months.Sum(m => m.Deaths),
            months.Sum(m => m.Treatments),
            months.Sum(m => m.TreatmentCostBdt),
            data.OnFarmAnimals,
            data.VaccinatedAnimals,
            coverage);
    }

    private static Dictionary<(int Year, int Month), HealthMonthAggregate> Index(IEnumerable<HealthMonthAggregate> rows) =>
        rows.GroupBy(r => (r.Year, r.Month))
            .ToDictionary(g => g.Key, g => new HealthMonthAggregate(g.Key.Year, g.Key.Month, g.Sum(r => r.Count), g.Sum(r => r.AmountBdt)));
}
