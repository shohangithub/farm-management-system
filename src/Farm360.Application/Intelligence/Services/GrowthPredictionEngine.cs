using Farm360.Application.Intelligence.Interfaces;
using Farm360.Domain.Intelligence.ValueObjects;
using Farm360.Domain.Livestock.Repositories;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Application.Intelligence.Services;

public class GrowthPredictionEngine : IGrowthPredictionEngine
{
    private readonly IAnimalRepository _animalRepository;
    private readonly IBreedRepository _breedRepository;

    public GrowthPredictionEngine(IAnimalRepository animalRepository, IBreedRepository breedRepository)
    {
        _animalRepository = animalRepository;
        _breedRepository = breedRepository;
    }

    public async Task<GrowthCurve?> CalculateGrowthCurveAsync(Guid animalId, CancellationToken cancellationToken = default)
    {
        var animal = await _animalRepository.GetByIdAsync(animalId, cancellationToken);
        if (animal is null)
            return null;

        var breed = await _breedRepository.GetByIdAsync(animal.BreedId, cancellationToken);
        var maxAdg = breed?.StandardAdgMax > 0 ? breed.StandardAdgMax : 1.5m; // Fallback max 1.5kg

        var weights = animal.WeightRecords.OrderBy(w => w.RecordedDate).ToList();
        
        if (weights.Count < 2)
        {
            // Use breed expected ADG as baseline when historical data is insufficient
            decimal baselineAdg = 0;
            if (breed != null)
            {
                // Defaulting to Average Farm condition ADG for baseline
                baselineAdg = breed.AdgAverageFarm > 0 ? breed.AdgAverageFarm : breed.StandardAdgMin;
            }

            var currentW = weights.LastOrDefault()?.Weight.WeightKg ?? weights.FirstOrDefault()?.Weight.WeightKg ?? 0m;
            var w30_baseline = currentW + (baselineAdg * 30);
            var w60_baseline = currentW + (baselineAdg * 60);
            var w90_baseline = currentW + (baselineAdg * 90);
            
            return new GrowthCurve(currentW, w30_baseline, w60_baseline, w90_baseline, baselineAdg);
        }

        var firstRecord = weights.First();
        var lastRecord = weights.Last();
        
        var totalDays = (lastRecord.RecordedDate.DayNumber - firstRecord.RecordedDate.DayNumber);
        
        decimal adg = 0;
        if (totalDays > 0)
        {
            var weightGain = lastRecord.Weight.WeightKg - firstRecord.Weight.WeightKg;
            adg = weightGain / (decimal)totalDays;
        }
        
        if (adg < 0) adg = 0; // Prevent negative projection
        if (adg > maxAdg) adg = maxAdg; // Cap at breed's maximum genetic potential
        
        var currentWeight = lastRecord.Weight.WeightKg;
        
        var w30 = currentWeight + (adg * 30);
        var w60 = currentWeight + (adg * 60);
        var w90 = currentWeight + (adg * 90);
        
        return new GrowthCurve(currentWeight, w30, w60, w90, adg);
    }
}
