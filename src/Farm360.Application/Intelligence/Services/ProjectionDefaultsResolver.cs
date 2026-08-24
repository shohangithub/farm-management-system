using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Contracts.Intelligence;
using Farm360.Domain.Livestock.Repositories;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.MasterData.Repositories;

namespace Farm360.Application.Intelligence.Services;

public class ProjectionDefaultsResolver : IProjectionDefaultsResolver
{
    private readonly IAnimalRepository _animalRepository;
    private readonly IBreedRepository _breedRepository;
    private readonly IInventoryItemRepository _inventoryItemRepository;

    public ProjectionDefaultsResolver(
        IAnimalRepository animalRepository,
        IBreedRepository breedRepository,
        IInventoryItemRepository inventoryItemRepository)
    {
        _animalRepository = animalRepository;
        _breedRepository = breedRepository;
        _inventoryItemRepository = inventoryItemRepository;
    }

    public async Task<ProjectionDefaultsDto> ResolveDefaultsAsync(Guid animalId, CancellationToken cancellationToken)
    {
        var animal = await _animalRepository.GetByIdAsync(animalId, cancellationToken)
            ?? throw new ArgumentException($"Animal {animalId} not found.");

        var breed = await _breedRepository.GetByIdAsync(animal.BreedId, cancellationToken);

        // 1. Starting live weight
        var startingWeight = animal.LatestWeightKg > 0
            ? new ProjectionDefaultValueDto<decimal>(animal.LatestWeightKg.Value, ProjectionSourceCode.AnimalRecord, "Latest Animal Weight")
            : new ProjectionDefaultValueDto<decimal>(200m, ProjectionSourceCode.SystemDefault, "System Default"); // Fallback

        // 2. Purchase price
        var purchasePrice = animal.AcquisitionPriceBdt.HasValue
            ? new ProjectionDefaultValueDto<decimal>(animal.AcquisitionPriceBdt.Value, ProjectionSourceCode.AnimalRecord, "Acquisition Price")
            : new ProjectionDefaultValueDto<decimal>(0m, ProjectionSourceCode.ManualOverride, "Manual Input Needed");

        // 3. ADG
        ProjectionDefaultValueDto<decimal> adg;
        if (animal.AdgKgPerDay.HasValue && animal.AdgKgPerDay > 0)
        {
            adg = new ProjectionDefaultValueDto<decimal>(animal.AdgKgPerDay.Value, ProjectionSourceCode.AnimalRecord, "Current Animal ADG");
        }
        else if (breed != null && breed.AdgAverageFarm > 0)
        {
            adg = new ProjectionDefaultValueDto<decimal>(breed.AdgAverageFarm, ProjectionSourceCode.BreedStandard, "Breed Average Farm ADG");
        }
        else
        {
            adg = new ProjectionDefaultValueDto<decimal>(0.7m, ProjectionSourceCode.SystemDefault, "System Default ADG");
        }

        // 4. Feed price
        // Getting average cost of active feed inventory items
        decimal avgFeedCost = 45m; // Fallback
        var feedPrice = new ProjectionDefaultValueDto<decimal>(avgFeedCost, ProjectionSourceCode.FarmSetting, "Default Feed Price");

        // 5. Daily feed qty
        // FCR mid-point if breed available
        decimal fcr = breed != null ? (breed.FcrMin + breed.FcrMax) / 2m : 8m;
        if (fcr == 0) fcr = 8m;
        var feedQty = new ProjectionDefaultValueDto<decimal>(adg.Value * fcr, ProjectionSourceCode.BreedStandard, "Calculated from Breed FCR");
        // 6. Meat price
        var meatPrice = new ProjectionDefaultValueDto<decimal>(680m, ProjectionSourceCode.FarmSetting, "Farm Default Meat Price");

        // 7. Meat yield ratios
        decimal yieldRatio = 0.50m;
        var yieldVal = new ProjectionDefaultValueDto<decimal>(yieldRatio, ProjectionSourceCode.SystemDefault, "Dressing Percentage");

        // 8. Grass / other / labor
        var grassCost = new ProjectionDefaultValueDto<decimal>(20m, ProjectionSourceCode.SystemDefault, "System Default");
        var otherCost = new ProjectionDefaultValueDto<decimal>(30m, ProjectionSourceCode.SystemDefault, "System Default");
        var laborCost = new ProjectionDefaultValueDto<decimal>(1500m, ProjectionSourceCode.SystemDefault, "System Default");

        // 9. Fattening period
        var period = new ProjectionDefaultValueDto<int>(120, ProjectionSourceCode.SystemDefault, "System Default");

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
            FatteningPeriodDays: period
        );
    }
}
