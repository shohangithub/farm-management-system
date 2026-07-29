using Farm360.Application.Intelligence.Interfaces;
using Farm360.Domain.Livestock.Repositories;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Application.Intelligence.Services;

public class CostAndProfitEngine : ICostAndProfitEngine
{
    private readonly IAnimalRepository _animalRepository;

    public CostAndProfitEngine(IAnimalRepository animalRepository)
    {
        _animalRepository = animalRepository;
    }

    public async Task<CostAndProfitSnapshot?> CalculateSnapshotAsync(Guid animalId, CancellationToken cancellationToken = default)
    {
        var animal = await _animalRepository.GetByIdAsync(animalId, cancellationToken);
        if (animal == null) return null;

        // Dummy logic for Phase 3 since Cost & Profit engine full logic relies on finance/inventory integration
        var estimatedDailyFeedCost = 150m; // 150 BDT / day
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
