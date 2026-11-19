using Farm360.Application.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Analytics.Queries;
using Farm360.Application.Analytics.Services;
using Farm360.Application.Finance.Services;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Health.Enums;
using Farm360.Persistence.Context;
using Farm360.Persistence.Repositories.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Persistence.Queries;

public class AnalyticsQueryService : IAnalyticsQueryService
{
    private readonly IFarmClock _clock;

    private readonly ApplicationDbContext _context;
    private readonly IAnimalFeedAllocationRepository _feedAllocations;

    /// <summary>Months shown on the trend charts (current month included).</summary>
    private const int TrendMonths = 6;

    public AnalyticsQueryService(ApplicationDbContext context, IAnimalFeedAllocationRepository feedAllocations,
        IFarmClock? clock = null)
    {
        _clock = clock ?? Farm360.Application.Common.Time.FarmClock.System;
        _context = context;
        _feedAllocations = feedAllocations;
    }

    private static List<(string Label, DateOnly Start, DateOnly End)> TrendWindow(DateOnly today)
    {
        var months = new List<(string Label, DateOnly Start, DateOnly End)>();
        var currentStart = new DateOnly(today.Year, today.Month, 1);
        for (int i = TrendMonths - 1; i >= 0; i--)
        {
            var start = currentStart.AddMonths(-i);
            // The current month runs to today only, so a part-month is not averaged over future days.
            var end = i == 0 ? today : start.AddMonths(1).AddDays(-1);
            months.Add((start.ToString("MMM", CultureInfo.InvariantCulture), start, end));
        }
        return months;
    }

    public async Task<BreedingAnalyticsDto> GetBreedingAnalyticsAsync(Guid? farmId, CancellationToken cancellationToken = default)
    {
        // BreedingRecord is not an ITenantEntity: scope it through the tenant-filtered Animals set.
        var breedingQuery = _context.BreedingRecords.AsNoTracking()
            .Where(br => _context.Animals.Any(a => a.Id == br.AnimalId));
        
        if (farmId.HasValue)
        {
            breedingQuery = breedingQuery.Where(br => _context.Animals.Any(a => a.Id == br.AnimalId && a.FarmId == farmId.Value));
        }

        var totalMatings = await breedingQuery.CountAsync(cancellationToken);
        var confirmedPregnancies = await breedingQuery.CountAsync(br => br.IsPregnancyConfirmed, cancellationToken);
        
        var next30Days = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        var today = _clock.Today;
        
        // Expected calvings are an alert: only dams still on the farm and not yet calved.
        var expectedCalvings = await breedingQuery
            .Where(br => br.ActualCalvingDate == null &&
                         _context.Animals.Any(a => a.Id == br.AnimalId && AnimalStatusGroups.OnFarm.Contains(a.Status)))
            .CountAsync(br => br.ExpectedCalvingDate >= today && br.ExpectedCalvingDate <= next30Days, cancellationToken);

        var conceptionRate = totalMatings == 0 ? 0 : (double)confirmedPregnancies / totalMatings * 100.0;

        return new BreedingAnalyticsDto(
            confirmedPregnancies,
            expectedCalvings,
            Math.Round(conceptionRate, 2));
    }

    // Revenue/expense = profit & loss heads only, classified by AccountChart (the same code the
    // finance dashboard uses). Investor capital, loans and stock/animal purchases are not revenue
    // or expense; income-type rows on expense accounts reduce expense.
    public async Task<FinanceAnalyticsDto> GetFinanceAnalyticsAsync(Guid? farmId, int year, CancellationToken cancellationToken = default)
    {
        var start = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var postings = await DashboardMetricQueries.LoadProfitAndLossPostingsAsync(
            _context, null, farmId, start, start.AddYears(1), cancellationToken);

        var byMonth = postings.ToLookup(t => t.TransactionDate.Month);

        var monthlyData = new List<MonthlyRevenueExpenseDto>();
        for (int i = 1; i <= 12; i++)
        {
            var month = byMonth[i];
            monthlyData.Add(new MonthlyRevenueExpenseDto(
                i,
                year,
                AccountChart.Revenue(month),
                AccountChart.Expenses(month)));
        }

        return new FinanceAnalyticsDto(monthlyData);
    }

    public async Task<HealthAnalyticsDto> GetHealthAnalyticsAsync(Guid? farmId, CancellationToken cancellationToken = default)
    {
        var oneYearAgo = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1));
        
        var mortalityQuery = _context.MortalityRecords.AsNoTracking()
            .Where(m => m.DeathDate >= oneYearAgo);

        if (farmId.HasValue)
        {
            mortalityQuery = mortalityQuery.Where(m => _context.Animals.Any(a => a.Id == m.AnimalId && a.FarmId == farmId.Value));
        }

        var deaths = await mortalityQuery.CountAsync(cancellationToken);

        // Vaccination compliance: % of animals on the farm vaccinated in the last 6 months.
        var sixMonthsAgo = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-6));

        var onFarm = DashboardMetricQueries.OnFarmAnimals(_context, null, farmId);
        var onFarmCount = await onFarm.CountAsync(cancellationToken);

        int vaccinatedAnimals = 0;
        if (onFarmCount > 0)
        {
            vaccinatedAnimals = await onFarm
                .Where(a => _context.VaccinationEvents.Any(v =>
                    v.AnimalId == a.Id && v.Status == VaccinationStatus.Completed && v.AdministeredDate >= sixMonthsAgo))
                .CountAsync(cancellationToken);
        }

        var compliance = onFarmCount == 0 ? 0 : (double)vaccinatedAnimals / onFarmCount * 100.0;

        return new HealthAnalyticsDto(
            deaths,
            Math.Round(compliance, 2));
    }

    public async Task<HerdCompositionDto> GetHerdCompositionAsync(Guid? farmId, CancellationToken cancellationToken = default)
    {
        var query = _context.Animals.AsNoTracking();
        if (farmId.HasValue) query = query.Where(a => a.FarmId == farmId.Value);

        var animals = await query.Select(a => new { a.Species, a.BreedId, a.Sex, a.Status }).ToListAsync(cancellationToken);

        // Species/breed/sex describe the herd on the farm now; status keeps the full history
        // (sold, dead, ...) so the status breakdown still shows where animals went.
        var onFarm = animals.Where(a => AnimalStatusGroups.IsOnFarm(a.Status)).ToList();
        var bySpecies = onFarm.GroupBy(a => a.Species.ToString()).ToDictionary(g => g.Key, g => g.Count());
        var byBreed = onFarm.GroupBy(a => a.BreedId.ToString()).ToDictionary(g => g.Key, g => g.Count());
        var bySex = onFarm.GroupBy(a => a.Sex.ToString()).ToDictionary(g => g.Key, g => g.Count());
        var byStatus = animals.GroupBy(a => a.Status.ToString()).ToDictionary(g => g.Key, g => g.Count());

        return new HerdCompositionDto(bySpecies, byBreed, bySex, byStatus);
    }

    /// <summary>
    /// Monthly herd ADG per active batch (or per species when there are no batches). Each month's
    /// value is the mean of per-animal gains (an animal's own first-to-last weigh-in in that month
    /// divided by the days between them); weigh-ins of different animals are never mixed. Months
    /// without such data are null: no value is estimated.
    /// </summary>
    public async Task<IReadOnlyList<AdgTrendDto>> GetAdgTrendsAsync(Guid? farmId, CancellationToken cancellationToken = default)
    {
        var months = TrendWindow(_clock.Today);
        var earliestDate = months[0].Start;

        // Every animal of the farm (not only today's Active ones): an animal sold last month still
        // grew on this farm during the months it was here.
        var animalsQuery = _context.Animals.AsNoTracking();
        if (farmId.HasValue)
        {
            animalsQuery = animalsQuery.Where(a => a.FarmId == farmId.Value);
        }

        var animals = await animalsQuery
            .Select(a => new { a.Id, a.BatchId, a.Species })
            .ToListAsync(cancellationToken);

        if (animals.Count == 0)
        {
            return Array.Empty<AdgTrendDto>();
        }

        var animalIds = animals.Select(a => a.Id).ToList();
        var weighIns = (await _context.WeightRecords.AsNoTracking()
                .Where(w => animalIds.Contains(w.AnimalId) && w.RecordedDate >= earliestDate)
                .Select(w => new { w.AnimalId, w.RecordedDate, WeightKg = w.Weight.WeightKg })
                .ToListAsync(cancellationToken))
            .Select(w => new HerdMetrics.WeighIn(w.AnimalId, w.RecordedDate, w.WeightKg))
            .ToList();

        var batchesQuery = _context.AnimalBatches.AsNoTracking().Where(b => b.Status == BatchStatus.Active);
        if (farmId.HasValue)
        {
            batchesQuery = batchesQuery.Where(b => b.FarmId == farmId.Value);
        }
        var batches = await batchesQuery
            .OrderBy(b => b.Name)
            .Select(b => new { b.Id, b.Name })
            .Take(4)
            .ToListAsync(cancellationToken);

        var groups = batches.Count > 0
            ? batches.Select(b => (Key: b.Id.ToString(), Name: b.Name, Ids: animals.Where(a => a.BatchId == b.Id).Select(a => a.Id).ToHashSet())).ToList()
            : animals.GroupBy(a => a.Species).Take(3)
                .Select(g => (Key: g.Key.ToString(), Name: $"{g.Key} Herd", Ids: g.Select(a => a.Id).ToHashSet())).ToList();

        var result = new List<AdgTrendDto>();
        foreach (var group in groups)
        {
            var groupWeighIns = weighIns.Where(w => group.Ids.Contains(w.AnimalId)).ToList();
            var points = months
                .Select(m => new AdgTrendPointDto(m.Label, HerdMetrics.AverageDailyGain(groupWeighIns, m.Start, m.End)))
                .ToList();

            // A series with no measurable month is omitted rather than drawn as a flat invented line.
            if (points.Any(p => p.AdgValue.HasValue))
            {
                result.Add(new AdgTrendDto(group.Key, group.Name, points));
            }
        }

        return result;
    }

    /// <summary>
    /// Monthly feed cost per head: confirmed feed allocated to animals that month (the same
    /// AnimalFeedAllocations totals every feed-cost report uses) divided by the average number of
    /// animals on the farm that month (head-days / days, from acquisition and sale/death dates).
    /// Months with no allocated feed or no animals are null. There is no budget source, so there
    /// is no budget series.
    /// </summary>
    public async Task<IReadOnlyList<FeedCostTrendDto>> GetFeedCostTrendsAsync(Guid? farmId, CancellationToken cancellationToken = default)
    {
        var months = TrendWindow(_clock.Today);

        var farmIds = farmId.HasValue
            ? new List<Guid> { farmId.Value }
            : await _context.Farms.AsNoTracking().Select(f => f.Id).ToListAsync(cancellationToken);

        var residencies = (await _context.Animals.AsNoTracking()
                .Where(a => farmIds.Contains(a.FarmId))
                .Select(a => new { a.AcquisitionDate, a.Status, a.SaleDate, a.DeathDate, a.ModifiedAtUtc })
                .ToListAsync(cancellationToken))
            .Select(a => new HerdMetrics.Residency(
                a.AcquisitionDate,
                HerdMetrics.ExitDate(a.Status, a.SaleDate, a.DeathDate, a.ModifiedAtUtc)))
            .ToList();

        var points = new List<FeedCostTrendPointDto>();
        foreach (var m in months)
        {
            decimal monthCost = 0m;
            foreach (var id in farmIds)
            {
                var totals = await _feedAllocations.GetTotalsForFarmAsync(id, m.Start, m.End, cancellationToken);
                monthCost += totals.Sum(t => t.TotalCostBdt);
            }

            var avgHead = HerdMetrics.AverageHeadCount(residencies, m.Start, m.End);
            decimal? perHead = monthCost > 0m && avgHead > 0m ? Math.Round(monthCost / avgHead, 2) : null;
            points.Add(new FeedCostTrendPointDto(m.Label, perHead));
        }

        if (points.All(p => p.CostPerAnimal is null))
        {
            return Array.Empty<FeedCostTrendDto>();
        }

        return [new FeedCostTrendDto("Feed Cost / Head (BDT)", points)];
    }

    public async Task<VaccinationComplianceDto> GetVaccinationComplianceAsync(Guid? farmId, CancellationToken cancellationToken = default)
    {
        var today = _clock.Today;

        var query = _context.VaccinationEvents.AsNoTracking();
        if (farmId.HasValue)
        {
            query = query.Where(v => _context.Animals.Any(a => a.Id == v.AnimalId && a.FarmId == farmId.Value));
        }

        var completed = await query.CountAsync(v => v.Status == VaccinationStatus.Completed, cancellationToken);
        // Due/overdue is work still to do: only for animals on the farm (AnimalStatusGroups.OnFarm).
        // Completed doses stay counted for animals since sold/dead (history).
        var pending = query.Where(v => _context.Animals.Any(a => a.Id == v.AnimalId && AnimalStatusGroups.OnFarm.Contains(a.Status)));
        var due = await pending.CountAsync(v => v.Status == VaccinationStatus.Scheduled && v.ScheduledDate <= today.AddDays(7) && v.ScheduledDate >= today, cancellationToken);
        var overdue = await pending.CountAsync(v => v.Status == VaccinationStatus.Overdue || (v.Status == VaccinationStatus.Scheduled && v.ScheduledDate < today), cancellationToken);

        return new VaccinationComplianceDto(completed, due, overdue);
    }

    public async Task<IReadOnlyList<FarmSummaryCardDto>> GetFarmSummaryCardsAsync(Guid farmId, CancellationToken cancellationToken = default)
    {
        // Scoped to the single farm named in the route -- this endpoint previously built a card
        // for EVERY farm in the tenant regardless of which farm's dashboard asked, leaking other
        // farms' animal counts and revenue onto a page that looks farm-specific.
        var farm = await _context.Farms.AsNoTracking().FirstOrDefaultAsync(f => f.Id == farmId, cancellationToken);
        if (farm is null)
        {
            return [];
        }

        // Same definitions as the executive dashboard cards: animals on farm, distinct sick animals
        // in open incidents, and AccountChart revenue for the current calendar month.
        var animalCount = await DashboardMetricQueries.OnFarmAnimals(_context, null, farm.Id).CountAsync(cancellationToken);
        var sickCount = await DashboardMetricQueries.CountSickAnimalsAsync(_context, null, farm.Id, cancellationToken);
        var (monthStart, nextMonthStart) = DashboardMetricQueries.CurrentMonth(DateTime.UtcNow);
        var monthlyRevenue = AccountChart.Revenue(await DashboardMetricQueries.LoadProfitAndLossPostingsAsync(
            _context, null, farm.Id, monthStart, nextMonthStart, cancellationToken));

        return [new FarmSummaryCardDto(farm.Id, farm.FarmName, animalCount, sickCount, monthlyRevenue)];
    }

    public async Task<decimal> GetTotalWeightGainKgAsync(Guid farmId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var animalIds = await _context.Animals.AsNoTracking()
            .Where(a => a.FarmId == farmId)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        if (animalIds.Count == 0)
        {
            return 0m;
        }

        var records = await _context.WeightRecords.AsNoTracking()
            .Where(w => animalIds.Contains(w.AnimalId) && w.RecordedDate >= from && w.RecordedDate <= to)
            .OrderBy(w => w.RecordedDate)
            .Select(w => new { w.AnimalId, w.RecordedDate, WeightKg = w.Weight.WeightKg })
            .ToListAsync(cancellationToken);

        decimal totalGain = 0m;
        foreach (var group in records.GroupBy(r => r.AnimalId))
        {
            var first = group.First();
            var last = group.Last();
            if (last.RecordedDate > first.RecordedDate)
            {
                totalGain += Math.Max(0m, last.WeightKg - first.WeightKg);
            }
        }

        return totalGain;
    }

    public async Task<IReadOnlyList<ActivityFeedItemDto>> GetRecentActivityFeedAsync(Guid? farmId, int count = 20, CancellationToken cancellationToken = default)
    {
        var query = _context.AuditLogs.AsNoTracking();
        
        var recentLogs = await query.OrderByDescending(a => a.OccurredAtUtc)
                                    .Take(count)
                                    .ToListAsync(cancellationToken);

        return recentLogs.Select(l => new ActivityFeedItemDto(
            l.Id,
            l.Action,
            l.EntityName,
            $"{l.Action} on {l.EntityName}",
            l.ChangedBy?.ToString() ?? "System",
            l.OccurredAtUtc
        )).ToList();
    }
}
