using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Contracts.Intelligence;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Livestock.Repositories;
using Farm360.Domain.MasterData.Repositories;

namespace Farm360.Application.Intelligence.Services;

public class ProjectionDefaultsResolver : IProjectionDefaultsResolver
{
    private const decimal FallbackFeedPriceBdtPerKg = 45m;
    private const int RecentFeedPriceWindowDays = 30;

    private readonly IAnimalRepository _animalRepository;
    private readonly IBreedRepository _breedRepository;
    private readonly IAnimalCostLedgerRepository _ledgerRepository;
    private readonly IAnimalFeedAllocationRepository _feedAllocationRepository;
    private readonly IAnimalFeedingPlanRepository _feedingPlanRepository;
    private readonly IAnimalValuationService _valuation;

    public ProjectionDefaultsResolver(
        IAnimalRepository animalRepository,
        IBreedRepository breedRepository,
        IAnimalCostLedgerRepository ledgerRepository,
        IAnimalFeedAllocationRepository feedAllocationRepository,
        IAnimalFeedingPlanRepository feedingPlanRepository,
        IAnimalValuationService valuation)
    {
        _animalRepository = animalRepository;
        _breedRepository = breedRepository;
        _ledgerRepository = ledgerRepository;
        _feedAllocationRepository = feedAllocationRepository;
        _feedingPlanRepository = feedingPlanRepository;
        _valuation = valuation;
    }

    public async Task<ProjectionDefaultsDto> ResolveDefaultsAsync(Guid animalId, CancellationToken cancellationToken)
    {
        var animal = await _animalRepository.GetByIdAsync(animalId, cancellationToken)
            ?? throw new ArgumentException($"Animal {animalId} not found.");

        var breed = await _breedRepository.GetByIdAsync(animal.BreedId, cancellationToken);
        var ledger = await _ledgerRepository.GetByAnimalIdAsync(animalId, cancellationToken);

        // 1. Starting live weight
        var startingWeight = animal.LatestWeightKg > 0
            ? new ProjectionDefaultValueDto<decimal>(animal.LatestWeightKg.Value, ProjectionSourceCode.AnimalRecord, "Latest recorded weight")
            : new ProjectionDefaultValueDto<decimal>(200m, ProjectionSourceCode.SystemDefault, "System default — no weight recorded yet");

        // 2. Purchase price
        var purchaseCost = _valuation.PurchaseCostBdt(animal, ledger);
        var purchasePrice = purchaseCost > 0
            ? new ProjectionDefaultValueDto<decimal>(purchaseCost, ProjectionSourceCode.AnimalRecord, "Acquisition price")
            : new ProjectionDefaultValueDto<decimal>(0m, ProjectionSourceCode.ManualOverride, "Manual input needed");

        // 3. ADG
        ProjectionDefaultValueDto<decimal> adg;
        if (animal.AdgKgPerDay.HasValue && animal.AdgKgPerDay > 0)
        {
            adg = new ProjectionDefaultValueDto<decimal>(animal.AdgKgPerDay.Value, ProjectionSourceCode.AnimalRecord, "Animal's measured ADG");
        }
        else if (breed != null && breed.AdgAverageFarm > 0)
        {
            adg = new ProjectionDefaultValueDto<decimal>(breed.AdgAverageFarm, ProjectionSourceCode.BreedStandard, "Breed average-farm ADG");
        }
        else
        {
            adg = new ProjectionDefaultValueDto<decimal>(0.7m, ProjectionSourceCode.SystemDefault, "System default ADG");
        }

        // 4. Feed price — what this animal's feed has actually cost per kg, most recent month first.
        var feedPrice = await ResolveFeedPriceAsync(animalId, cancellationToken);

        // 5. Daily feed quantity — today's ration from the animal's active feeding plans (base chart
        //    plus any extras), falling back to ADG × breed FCR when it has no plan.
        var feedQty = await ResolveDailyFeedAsync(animal.TenantId, animalId, adg.Value, breed?.FcrMin, breed?.FcrMax, cancellationToken);

        // 6. Meat price & 7. dressing yield — from the farm's business rules.
        var meatPrice = new ProjectionDefaultValueDto<decimal>(_valuation.MeatPricePerKgBdt, ProjectionSourceCode.FarmSetting, "Farm default meat price");

        var yieldRatio = _valuation.DressingRatio;
        var yieldVal = new ProjectionDefaultValueDto<decimal>(yieldRatio, ProjectionSourceCode.FarmSetting, "Farm dressing percentage");

        // 8. Grass / other / labor
        var grassCost = new ProjectionDefaultValueDto<decimal>(20m, ProjectionSourceCode.SystemDefault, "System default");
        var otherCost = new ProjectionDefaultValueDto<decimal>(30m, ProjectionSourceCode.SystemDefault, "System default");
        var laborCost = new ProjectionDefaultValueDto<decimal>(1500m, ProjectionSourceCode.SystemDefault, "System default");

        // 9. Fattening period
        var period = new ProjectionDefaultValueDto<int>(120, ProjectionSourceCode.SystemDefault, "System default");

        // 10. Money already sunk into the animal — the cost ledger (whose feed bucket mirrors the
        //     confirmed feed allocations), or the purchase price plus allocated feed without one.
        var fallbackFeed = ledger is null
            ? (await _feedAllocationRepository.GetTotalsForAnimalAsync(animalId, DateOnly.MinValue, DateOnly.MaxValue, cancellationToken)).TotalCostBdt
            : 0m;
        var cost = _valuation.CostToDate(animal, ledger, fallbackFeed);
        var costToDate = new ProjectionDefaultValueDto<decimal>(
            Math.Round(cost.TotalBdt, 2),
            ledger is null ? ProjectionSourceCode.SystemDefault : ProjectionSourceCode.AnimalRecord,
            ledger is null
                ? "Purchase price + allocated feed (no cost ledger yet)"
                : "Cost to date (acquisition + feed + vet + labour + overhead)");

        // 11. Live-weight valuation rate under the farm's valuation policy.
        var liveRate = new ProjectionDefaultValueDto<decimal>(
            _valuation.LiveWeightRatePerKgBdt, ProjectionSourceCode.FarmSetting, _valuation.RateBasisDescription);

        return new ProjectionDefaultsDto(
            StartingLiveWeightKg: startingWeight,
            PurchasePriceBdt: purchasePrice,
            CurrentMeatPriceBdtPerKg: meatPrice,
            InitialMeatYieldRatio: yieldVal,
            DailyLiveWeightGainKg: adg,
            MeatYieldOnDailyGainRatio: yieldVal,
            DailyFeedQuantityKgAtStart: feedQty,
            FeedPriceBdtPerKg: feedPrice,
            DailyGrassCostBdt: grassCost,
            DailyOtherCostBdt: otherCost,
            MonthlyLaborCostBdt: laborCost,
            FatteningPeriodDays: period,
            CostToDateBdt: costToDate,
            LiveWeightValuePerKgBdt: liveRate
        );
    }

    private async Task<ProjectionDefaultValueDto<decimal>> ResolveFeedPriceAsync(Guid animalId, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var recent = await _feedAllocationRepository.GetTotalsForAnimalAsync(
            animalId, today.AddDays(-RecentFeedPriceWindowDays), today, cancellationToken);
        if (recent.TotalKg > 0 && recent.TotalCostBdt > 0)
        {
            return new ProjectionDefaultValueDto<decimal>(
                Math.Round(recent.TotalCostBdt / recent.TotalKg, 2), ProjectionSourceCode.InventoryAverage,
                $"Average cost of feed eaten in the last {RecentFeedPriceWindowDays} days");
        }

        var lifetime = await _feedAllocationRepository.GetTotalsForAnimalAsync(
            animalId, DateOnly.MinValue, DateOnly.MaxValue, cancellationToken);
        if (lifetime.TotalKg > 0 && lifetime.TotalCostBdt > 0)
        {
            return new ProjectionDefaultValueDto<decimal>(
                Math.Round(lifetime.TotalCostBdt / lifetime.TotalKg, 2), ProjectionSourceCode.InventoryAverage,
                "Average cost of all feed this animal has eaten");
        }

        return new ProjectionDefaultValueDto<decimal>(FallbackFeedPriceBdtPerKg, ProjectionSourceCode.SystemDefault, "System default feed price");
    }

    private async Task<ProjectionDefaultValueDto<decimal>> ResolveDailyFeedAsync(
        Guid tenantId, Guid animalId, decimal adg, decimal? fcrMin, decimal? fcrMax, CancellationToken cancellationToken)
    {
        var plans = await _feedingPlanRepository.GetActivePlansForAnimalAsync(tenantId, animalId, cancellationToken);
        var rationKg = plans
            .Where(p => p.Status == FeedingPlanStatus.Active)
            .Sum(p => p.CurrentConcentrateKgPerDay ?? 0m);
        if (rationKg > 0)
        {
            return new ProjectionDefaultValueDto<decimal>(Math.Round(rationKg, 3), ProjectionSourceCode.AnimalRecord, "Current ration from active feeding plans");
        }

        decimal fcr = fcrMin.HasValue && fcrMax.HasValue ? (fcrMin.Value + fcrMax.Value) / 2m : 0m;
        if (fcr <= 0) fcr = 8m;
        return new ProjectionDefaultValueDto<decimal>(Math.Round(adg * fcr, 3), ProjectionSourceCode.BreedStandard, "ADG × breed FCR (no active feeding plan)");
    }
}
