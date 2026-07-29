using Farm360.Application.Intelligence.Interfaces;
using Farm360.Domain.Intelligence;
using Farm360.Domain.Intelligence.Enums;
using Farm360.Domain.Intelligence.Interfaces.Repositories;
using Farm360.Domain.Livestock.Repositories;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Application.Intelligence.Services;

public class RuleEngine : IRuleEngine
{
    private readonly IAnimalRepository _animalRepository;
    private readonly IPerformanceTargetRepository _performanceTargetRepository;
    private readonly IGrowthPredictionEngine _growthPredictionEngine;

    public RuleEngine(
        IAnimalRepository animalRepository,
        IPerformanceTargetRepository performanceTargetRepository,
        IGrowthPredictionEngine growthPredictionEngine)
    {
        _animalRepository = animalRepository;
        _performanceTargetRepository = performanceTargetRepository;
        _growthPredictionEngine = growthPredictionEngine;
    }

    public async Task<List<ActionableInsight>> EvaluateAnimalPerformanceAsync(Guid animalId, CancellationToken cancellationToken = default)
    {
        var insights = new List<ActionableInsight>();
        var animal = await _animalRepository.GetByIdAsync(animalId, cancellationToken);
        
        if (animal is null)
            return insights;

        var growthCurve = await _growthPredictionEngine.CalculateGrowthCurveAsync(animalId, cancellationToken);
        if (growthCurve is null || growthCurve.CurrentAdgKg == 0)
            return insights; // Not enough data yet

        // Simplified for this phase: assuming we match on Breed and Stage = "Grower"
        // In a real system, Stage would be calculated based on Age.
        var target = await _performanceTargetRepository.GetTargetForBreedAndStageAsync(
            animal.BreedName, 
            "Grower", 
            cancellationToken);

        if (target != null)
        {
            if (growthCurve.CurrentAdgKg < target.TargetAdgKg)
            {
                var diff = Math.Round(target.TargetAdgKg - growthCurve.CurrentAdgKg, 2);
                var insight = new ActionableInsight(
                    id: Guid.NewGuid(),
                    tenantId: animal.TenantId,
                    farmId: animal.FarmId,
                    type: InsightType.Nutrition,
                    severity: InsightSeverity.Warning,
                    title: "Underperforming Growth Detected",
                    message: $"Current ADG ({growthCurve.CurrentAdgKg}kg) is below the target ({target.TargetAdgKg}kg). Consider increasing feed energy/protein by 10%.",
                    animalId: animal.Id
                );
                insights.Add(insight);
            }
            else
            {
                var insight = new ActionableInsight(
                    id: Guid.NewGuid(),
                    tenantId: animal.TenantId,
                    farmId: animal.FarmId,
                    type: InsightType.Growth,
                    severity: InsightSeverity.Success,
                    title: "Growth On Track",
                    message: $"Current ADG ({growthCurve.CurrentAdgKg}kg) meets or exceeds target ({target.TargetAdgKg}kg). Maintain current feeding regimen.",
                    animalId: animal.Id
                );
                insights.Add(insight);
            }
        }

        return insights;
    }
}
