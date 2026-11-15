using Farm360.Application.Common.Interfaces;
using Farm360.Application.Feeding.Services;
using Farm360.Domain.BusinessRules;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Feeding.Queries.DailyFeedingEntries;

/// <param name="AllocatedKg">Kg of this ingredient across every head fed.</param>
/// <param name="UnitCostBdt">Cost per kg of this ingredient (at consumption for a confirmed entry).</param>
public record FeedingEntryIngredientCostDto(
    Guid IngredientId,
    string IngredientName,
    decimal Percentage,
    decimal AllocatedKg,
    decimal UnitCostBdt,
    decimal TotalCostBdt,
    string? InventoryItemName,
    string CostSource);

/// <param name="HeadCount">Heads the entry's quantity is multiplied by (1 for an animal's own plan or a WholeGroup quantity).</param>
/// <param name="TotalKg">Quantity x heads: what the ingredient rows add up to.</param>
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
    IReadOnlyList<FeedingEntryIngredientCostDto> Ingredients,
    decimal HeadCount = 1m,
    decimal TotalKg = 0m);

public sealed record GetFeedingEntryCostBreakdownQuery(Guid EntryId) : IRequest<FeedingEntryCostBreakdownDto?>;

/// <summary>
/// Splits an entry's cost into its ingredients so the rows add up to the header: quantity x heads
/// fed, split by normalised recipe %, and — for a confirmed entry — priced at what the stock cost
/// when it was deducted (the entry's own stock transactions), not today's average.
/// </summary>
public sealed class GetFeedingEntryCostBreakdownQueryHandler : IRequestHandler<GetFeedingEntryCostBreakdownQuery, FeedingEntryCostBreakdownDto?>
{
    private readonly IDailyFeedingEntryRepository _entryRepository;
    private readonly IFeedFormulaRepository _formulaRepository;
    private readonly IFeedIngredientRepository _feedIngredientRepository;
    private readonly IInventoryItemRepository _inventoryRepository;
    private readonly IAnimalFeedingPlanRepository? _planRepository;
    private readonly IAnimalFeedAllocationRepository? _allocationRepository;
    private readonly IStockTransactionRepository? _transactionRepository;
    private readonly IFarmBusinessRulesProvider? _rules;
    private readonly Farm360.Application.Inventory.Units.IUnitService? _units;

    public GetFeedingEntryCostBreakdownQueryHandler(
        IDailyFeedingEntryRepository entryRepository,
        IFeedFormulaRepository formulaRepository,
        IFeedIngredientRepository feedIngredientRepository,
        IInventoryItemRepository inventoryRepository,
        IAnimalFeedingPlanRepository? planRepository = null,
        IAnimalFeedAllocationRepository? allocationRepository = null,
        IStockTransactionRepository? transactionRepository = null,
        IFarmBusinessRulesProvider? rules = null,
        Farm360.Application.Inventory.Units.IUnitService? units = null)
    {
        _entryRepository = entryRepository;
        _formulaRepository = formulaRepository;
        _feedIngredientRepository = feedIngredientRepository;
        _inventoryRepository = inventoryRepository;
        _planRepository = planRepository;
        _allocationRepository = allocationRepository;
        _transactionRepository = transactionRepository;
        _rules = rules;
        _units = units;
    }

    public async Task<FeedingEntryCostBreakdownDto?> Handle(GetFeedingEntryCostBreakdownQuery request, CancellationToken cancellationToken)
    {
        var entry = await _entryRepository.GetByIdAsync(request.EntryId, cancellationToken);
        if (entry is null) return null;

        var formula = await _formulaRepository.GetByIdAsync(entry.FormulaId, cancellationToken);
        var formulaName = formula?.Title ?? "Formula N/A";

        var basis = _rules?.Current.GroupPlanQuantityBasis ?? GroupPlanQuantityBasis.PerHead;
        var heads = await FeedEntryHeads.MultiplierAsync(entry, _planRepository, _allocationRepository, basis, cancellationToken);
        var totalKg = (entry.ActualKg ?? entry.ExpectedKg) * heads;
        var confirmed = entry.Status is DailyFeedingEntryStatus.Confirmed or DailyFeedingEntryStatus.Adjusted;
        var ingredientDtos = new List<FeedingEntryIngredientCostDto>();

        if (formula != null && entry.Status != DailyFeedingEntryStatus.Skipped)
        {
            // Stock actually taken out for this entry, per item, at the cost it carried then.
            var deducted = new Dictionary<Guid, decimal>();
            if (confirmed && _transactionRepository is not null)
            {
                var txs = await _transactionRepository.GetByFarmIdAsync(entry.FarmId, entry.EntryDate, entry.EntryDate, cancellationToken);
                foreach (var tx in txs.Where(t => t.ReferenceId == entry.Id && t.TransactionType == StockTransactionType.PlannedFeedConsumption))
                    deducted[tx.InventoryItemId] = deducted.GetValueOrDefault(tx.InventoryItemId) + tx.Quantity * tx.UnitCostBdt;
            }

            var calculator = new FeedCostCalculator(_feedIngredientRepository, _inventoryRepository, _units);
            var requirement = await calculator.RequirementAsync(formula, totalKg, cancellationToken);

            foreach (var line in requirement.Ingredients)
            {
                decimal cost;
                string source;
                if (line.Item is null)
                {
                    cost = line.CostBdt;
                    source = "Recipe cost (not stock-tracked)";
                }
                else if (!confirmed)
                {
                    cost = line.CostBdt;
                    source = "Planned at current stock cost";
                }
                else if (deducted.TryGetValue(line.Item.Id, out var atConsumption))
                {
                    cost = atConsumption;
                    source = "Stock cost at consumption";
                }
                else
                {
                    cost = 0m;
                    source = "Not deducted from stock";
                }

                ingredientDtos.Add(new FeedingEntryIngredientCostDto(
                    line.IngredientId,
                    line.Name,
                    line.Percentage,
                    Math.Round(line.Kg, 4),
                    line.Kg > 0 ? Math.Round(cost / line.Kg, 4) : 0m,
                    Math.Round(cost, 2),
                    line.Item?.Name,
                    source));
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
            ingredientDtos,
            heads,
            Math.Round(totalKg, 4));
    }
}
