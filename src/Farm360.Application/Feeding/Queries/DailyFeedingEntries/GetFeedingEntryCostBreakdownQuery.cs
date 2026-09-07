using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Feeding.Queries.DailyFeedingEntries;

public record FeedingEntryIngredientCostDto(
    Guid IngredientId,
    string IngredientName,
    decimal Percentage,
    decimal AllocatedKg,
    decimal UnitCostBdt,
    decimal TotalCostBdt,
    string? InventoryItemName,
    string CostSource);

public record FeedingEntryCostBreakdownDto(
    Guid EntryId,
    Guid FormulaId,
    string FormulaName,
    DateOnly EntryDate,
    decimal ExpectedKg,
    decimal? ActualKg,
    decimal? BlendedUnitCostBdt,
    decimal? TotalCostBdt,
    string Status,
    IReadOnlyList<FeedingEntryIngredientCostDto> Ingredients);

public sealed record GetFeedingEntryCostBreakdownQuery(Guid EntryId) : IRequest<FeedingEntryCostBreakdownDto?>;

public sealed class GetFeedingEntryCostBreakdownQueryHandler : IRequestHandler<GetFeedingEntryCostBreakdownQuery, FeedingEntryCostBreakdownDto?>
{
    private readonly IDailyFeedingEntryRepository _entryRepository;
    private readonly IFeedFormulaRepository _formulaRepository;
    private readonly IFeedIngredientRepository _feedIngredientRepository;
    private readonly IInventoryItemRepository _inventoryRepository;

    public GetFeedingEntryCostBreakdownQueryHandler(
        IDailyFeedingEntryRepository entryRepository,
        IFeedFormulaRepository formulaRepository,
        IFeedIngredientRepository feedIngredientRepository,
        IInventoryItemRepository inventoryRepository)
    {
        _entryRepository = entryRepository;
        _formulaRepository = formulaRepository;
        _feedIngredientRepository = feedIngredientRepository;
        _inventoryRepository = inventoryRepository;
    }

    public async Task<FeedingEntryCostBreakdownDto?> Handle(GetFeedingEntryCostBreakdownQuery request, CancellationToken cancellationToken)
    {
        var entry = await _entryRepository.GetByIdAsync(request.EntryId, cancellationToken);
        if (entry is null) return null;

        var formula = await _formulaRepository.GetByIdAsync(entry.FormulaId, cancellationToken);
        var formulaName = formula?.Title ?? "Formula N/A";

        var kg = entry.ActualKg ?? entry.ExpectedKg;
        var ingredientDtos = new List<FeedingEntryIngredientCostDto>();

        if (formula != null)
        {
            foreach (var fi in formula.Ingredients)
            {
                var feedIngredient = await _feedIngredientRepository.GetByIdAsync(fi.IngredientId, cancellationToken);
                string ingredientName = feedIngredient?.Name ?? "Unknown Ingredient";
                string? inventoryItemName = null;
                string costSource = "Formula Standard";
                decimal unitCost = fi.IngredientCostPerKg;

                if (feedIngredient?.InventoryItemId != null)
                {
                    var inventoryItem = await _inventoryRepository.GetByIdAsync(feedIngredient.InventoryItemId.Value, cancellationToken);
                    if (inventoryItem != null)
                    {
                        inventoryItemName = inventoryItem.Name;
                        unitCost = inventoryItem.WeightedAverageCostBdt;
                        costSource = "Inventory WAC Snapshot";
                    }
                }

                var allocatedKg = Math.Round(kg * (fi.Percentage / 100m), 2);
                var totalIngredientCost = Math.Round(allocatedKg * unitCost, 2);

                ingredientDtos.Add(new FeedingEntryIngredientCostDto(
                    fi.IngredientId,
                    ingredientName,
                    fi.Percentage,
                    allocatedKg,
                    unitCost,
                    totalIngredientCost,
                    inventoryItemName,
                    costSource
                ));
            }
        }

        return new FeedingEntryCostBreakdownDto(
            entry.Id,
            entry.FormulaId,
            formulaName,
            entry.EntryDate,
            entry.ExpectedKg,
            entry.ActualKg,
            entry.UnitCostAtConsumptionBdt,
            entry.TotalCostBdt,
            entry.Status.ToString(),
            ingredientDtos
        );
    }
}
