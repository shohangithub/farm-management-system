using Farm360.Application.Intelligence.Interfaces;
using Farm360.Domain.Livestock.Repositories;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Application.Intelligence.Services;

public class CostAndProfitEngine : ICostAndProfitEngine
{
    private readonly IAnimalRepository _animalRepository;
    private readonly IBreedRepository _breedRepository;

    public CostAndProfitEngine(IAnimalRepository animalRepository, IBreedRepository breedRepository)
    {
        _animalRepository = animalRepository;
        _breedRepository = breedRepository;
    }

    public async Task<CostAndProfitSnapshot?> CalculateSnapshotAsync(Guid animalId, CancellationToken cancellationToken = default)
    {
        var animal = await _animalRepository.GetByIdAsync(animalId, cancellationToken);
        if (animal == null) return null;

        var estimatedDailyFeedCost = 150m; // Fallback: 150 BDT / day
        
        var breed = await _breedRepository.GetByIdAsync(animal.BreedId, cancellationToken);
        if (breed != null)
        {
            var targetAdg = breed.AdgAverageFarm > 0 ? breed.AdgAverageFarm : breed.StandardAdgMin;
            var fcr = breed.FcrMin > 0 ? breed.FcrMin : 8m; // Default FCR 8 if not set
            var feedCostPerKg = 50m; // 50 BDT per kg of dry matter (mocked for now)
            
            // Daily Feed Dry Matter (kg) = Target ADG * Breed FCR
            var dailyFeedDm = targetAdg * fcr;
            estimatedDailyFeedCost = dailyFeedDm * feedCostPerKg;
        }
        var acquisitionCost = animal.AcquisitionPriceBdt ?? 0;
        
        var daysOnFarm = (DateTime.UtcNow.Date - animal.CreatedAtUtc.Date).TotalDays;
        if (daysOnFarm < 0) daysOnFarm = 0;

        var feedCostSoFar = (decimal)daysOnFarm * estimatedDailyFeedCost;
        var totalInvestment = acquisitionCost + feedCostSoFar;

        return new CostAndProfitSnapshot(
            Projected30DayFeedCostBdt: estimatedDailyFeedCost * 30,
            TotalInvestmentBdt: totalInvestment
        );
    }
}
