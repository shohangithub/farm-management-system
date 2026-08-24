using Farm360.Application.Intelligence.Interfaces;
using Farm360.Domain.Livestock.Repositories;
using Farm360.Domain.Intelligence.Projections;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Application.Intelligence.Services;

public class SimulationEngine : ISimulationEngine
{
    private readonly IAnimalRepository _animalRepository;
    private readonly IProjectionDefaultsResolver _defaultsResolver;

    public SimulationEngine(
        IAnimalRepository animalRepository,
        IProjectionDefaultsResolver defaultsResolver)
    {
        _animalRepository = animalRepository;
        _defaultsResolver = defaultsResolver;
    }

    public async Task<SaleSimulationResult?> SimulateSaleAsync(Guid animalId, DateOnly targetDate, CancellationToken cancellationToken = default)
    {
        var animal = await _animalRepository.GetByIdAsync(animalId, cancellationToken);
        if (animal == null) return null;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var daysFromNow = (targetDate.ToDateTime(TimeOnly.MinValue) - today.ToDateTime(TimeOnly.MinValue)).Days;
        
        if (daysFromNow <= 0) return null;

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
            Math.Max(daysFromNow, defaults.FatteningPeriodDays.Value) // Ensure projection goes far enough
        );

        var result = FatteningProjectionCalculator.Calculate(inputs);

        var targetDayResult = result.Days.FirstOrDefault(d => d.Day == daysFromNow) ?? result.Days[^1];

        return new SaleSimulationResult(
            AnimalId: animalId,
            TargetDate: targetDate,
            DaysFromNow: daysFromNow,
            ProjectedWeightKg: targetDayResult.LiveWeightKg,
            EstimatedSalePriceBdt: targetDayResult.MeatValueBdt,
            ProjectedAdditionalCostBdt: targetDayResult.CumulativeCostBdt, // This represents cost from day 1 to daysFromNow
            ProjectedTotalCostBdt: targetDayResult.TotalInvestmentBdt,
            ProjectedProfitMarginBdt: targetDayResult.ProfitLossBdt
        );
    }
}

