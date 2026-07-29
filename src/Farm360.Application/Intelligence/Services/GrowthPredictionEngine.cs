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

    public GrowthPredictionEngine(IAnimalRepository animalRepository)
    {
        _animalRepository = animalRepository;
    }

    public async Task<GrowthCurve?> CalculateGrowthCurveAsync(Guid animalId, CancellationToken cancellationToken = default)
    {
        var animal = await _animalRepository.GetByIdAsync(animalId, cancellationToken);
        if (animal is null)
            return null;

        var weights = animal.WeightRecords.OrderBy(w => w.RecordedDate).ToList();
        
        if (weights.Count < 2)
        {
            // Cannot calculate ADG with less than 2 records.
            // Just use the latest weight or initial weight.
            var currentW = weights.LastOrDefault()?.Weight.WeightKg ?? weights.FirstOrDefault()?.Weight.WeightKg ?? 0m;
            return new GrowthCurve(currentW, currentW, currentW, currentW, 0);
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
        
        var currentWeight = lastRecord.Weight.WeightKg;
        
        var w30 = currentWeight + (adg * 30);
        var w60 = currentWeight + (adg * 60);
        var w90 = currentWeight + (adg * 90);
        
        return new GrowthCurve(currentWeight, w30, w60, w90, adg);
    }
}
