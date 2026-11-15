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
    private readonly IAnimalValuationService _valuation;

    public SimulationEngine(
        IAnimalRepository animalRepository,
        IProjectionDefaultsResolver defaultsResolver,
        IAnimalValuationService valuation)
    {
        _animalRepository = animalRepository;
        _defaultsResolver = defaultsResolver;
        _valuation = valuation;
    }

    public async Task<SaleSimulationResult?> SimulateSaleAsync(Guid animalId, DateOnly targetDate, CancellationToken cancellationToken = default)
    {
        var animal = await _animalRepository.GetByIdAsync(animalId, cancellationToken);
        if (animal == null) return null;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var daysFromNow = (targetDate.ToDateTime(TimeOnly.MinValue) - today.ToDateTime(TimeOnly.MinValue)).Days;
        
        if (daysFromNow <= 0) return null;

        var defaults = await _defaultsResolver.ResolveDefaultsAsync(animalId, cancellationToken);

        // The projection starts at today's weight, so day 0's investment is the cost to date —
        // not the bare purchase price, which would leave out all feed, vet and labour so far.
        var inputs = ProjectionInputsFactory.FromToday(defaults, Math.Max(daysFromNow, defaults.FatteningPeriodDays.Value));
        var result = FatteningProjectionCalculator.Calculate(inputs);

        var targetDayResult = result.Days.FirstOrDefault(d => d.Day == daysFromNow) ?? result.Days[^1];
        var totalCost = targetDayResult.TotalInvestmentBdt;
        var saleValue = _valuation.ValueAtWeight(targetDayResult.LiveWeightKg, totalCost);

        return new SaleSimulationResult(
            AnimalId: animalId,
            TargetDate: targetDate,
            DaysFromNow: daysFromNow,
            ProjectedWeightKg: targetDayResult.LiveWeightKg,
            EstimatedSalePriceBdt: saleValue,
            // Running cost from today to the sale date: feed + grass + other + labour.
            ProjectedAdditionalCostBdt: Math.Round(targetDayResult.CumulativeCostBdt, 2),
            // Cost to date + the running cost above.
            ProjectedTotalCostBdt: Math.Round(totalCost, 2),
            ProjectedProfitMarginBdt: Math.Round(saleValue - totalCost, 2)
        );
    }
}

