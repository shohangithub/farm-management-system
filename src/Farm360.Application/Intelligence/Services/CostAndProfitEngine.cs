using Farm360.Application.Intelligence.Interfaces;
using Farm360.Domain.Livestock.Repositories;
using Farm360.Domain.Intelligence.Projections;
using Farm360.Contracts.Intelligence;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Application.Intelligence.Services;

public class CostAndProfitEngine : ICostAndProfitEngine
{
    private readonly IAnimalRepository _animalRepository;
    private readonly IProjectionDefaultsResolver _defaultsResolver;

    public CostAndProfitEngine(IAnimalRepository animalRepository, IProjectionDefaultsResolver defaultsResolver)
    {
        _animalRepository = animalRepository;
        _defaultsResolver = defaultsResolver;
    }

    public async Task<CostAndProfitSnapshot?> CalculateSnapshotAsync(Guid animalId, CancellationToken cancellationToken = default)
    {
        var animal = await _animalRepository.GetByIdAsync(animalId, cancellationToken);
        if (animal == null) return null;

        var defaults = await _defaultsResolver.ResolveDefaultsAsync(animalId, cancellationToken);
        var inputs = new FatteningProjectionInputs(
            defaults.StartingLiveWeightKg.Value,
            defaults.PurchasePriceBdt.Value,
            defaults.CurrentMeatPriceBdtPerKg.Value,
            defaults.InitialMeatYieldRatio.Value,
            defaults.DailyLiveWeightGainKg.Value,
            defaults.MeatYieldOnDailyGainRatio.Value,
            defaults.DailyFeedQuantityKgAtStart.Value,
            defaults.FeedPriceBdtPerKg.Value,
            defaults.DailyGrassCostBdt.Value,
            defaults.DailyOtherCostBdt.Value,
            defaults.MonthlyLaborCostBdt.Value,
            defaults.FatteningPeriodDays.Value
        );

        var result = FatteningProjectionCalculator.Calculate(inputs);

        var daysOnFarm = (int)(DateTime.UtcNow.Date - animal.CreatedAtUtc.Date).TotalDays;
        if (daysOnFarm <= 0) daysOnFarm = 1;

        // Ensure we don't exceed projection limit for the current cost snapshot
        var currentDayResult = result.Days.FirstOrDefault(d => d.Day == Math.Min(daysOnFarm, inputs.FatteningPeriodDays)) ?? result.Days[^1];

        return new CostAndProfitSnapshot(
            Projected30DayFeedCostBdt: result.Summary.TotalFeedCostBdt * (30m / inputs.FatteningPeriodDays),
            TotalInvestmentBdt: currentDayResult.TotalInvestmentBdt
        );
    }
}

