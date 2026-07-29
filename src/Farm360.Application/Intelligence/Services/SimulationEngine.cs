using Farm360.Application.Intelligence.Interfaces;
using Farm360.Domain.Livestock.Repositories;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Application.Intelligence.Services;

public class SimulationEngine : ISimulationEngine
{
    private readonly IAnimalRepository _animalRepository;
    private readonly IGrowthPredictionEngine _growthPredictionEngine;
    private readonly ICostAndProfitEngine _costAndProfitEngine;

    public SimulationEngine(
        IAnimalRepository animalRepository,
        IGrowthPredictionEngine growthPredictionEngine,
        ICostAndProfitEngine costAndProfitEngine)
    {
        _animalRepository = animalRepository;
        _growthPredictionEngine = growthPredictionEngine;
        _costAndProfitEngine = costAndProfitEngine;
    }

    public async Task<SaleSimulationResult?> SimulateSaleAsync(Guid animalId, DateOnly targetDate, CancellationToken cancellationToken = default)
    {
        var animal = await _animalRepository.GetByIdAsync(animalId, cancellationToken);
        if (animal == null) return null;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var daysFromNow = (targetDate.ToDateTime(TimeOnly.MinValue) - today.ToDateTime(TimeOnly.MinValue)).Days;
        
        if (daysFromNow < 0) return null; // Can't simulate past sales here

        var growthCurve = await _growthPredictionEngine.CalculateGrowthCurveAsync(animalId, cancellationToken);
        var currentSnapshot = await _costAndProfitEngine.CalculateSnapshotAsync(animalId, cancellationToken);
        
        if (growthCurve == null || currentSnapshot == null) return null;

        var currentWeight = growthCurve.CurrentWeightKg;
        var adg = growthCurve.CurrentAdgKg;

        var projectedWeight = currentWeight + (adg * daysFromNow);
        
        // Very basic estimate: assume daily feed cost is constant for the simulation period
        // Real logic would calculate based on changing weight and FCR
        var estimatedDailyCost = currentSnapshot.Projected30DayFeedCostBdt / 30m;
        var projectedAdditionalCost = estimatedDailyCost * daysFromNow;
        var projectedTotalCost = currentSnapshot.TotalInvestmentBdt + projectedAdditionalCost;

        // Estimate sale price (assuming fixed rate per kg for this demo, e.g., 500 BDT/kg)
        // In real app, we'd query MarketPriceTracker
        var ratePerKg = 500m; 
        var estimatedSalePrice = projectedWeight * ratePerKg;
        
        var projectedProfitMargin = estimatedSalePrice - projectedTotalCost;

        return new SaleSimulationResult(
            AnimalId: animalId,
            TargetDate: targetDate,
            DaysFromNow: daysFromNow,
            ProjectedWeightKg: Math.Round(projectedWeight, 2),
            EstimatedSalePriceBdt: Math.Round(estimatedSalePrice, 2),
            ProjectedAdditionalCostBdt: Math.Round(projectedAdditionalCost, 2),
            ProjectedTotalCostBdt: Math.Round(projectedTotalCost, 2),
            ProjectedProfitMarginBdt: Math.Round(projectedProfitMargin, 2)
        );
    }
}
