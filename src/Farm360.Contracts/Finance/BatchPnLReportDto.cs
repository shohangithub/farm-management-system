using System;

namespace Farm360.Contracts.Finance;

/// <param name="TotalIncomeBdt">Realised revenue booked to the batch or any of its animals.</param>
/// <param name="TotalCostBdt">Full cost to date of every animal in the batch (cost ledger; purchase price when an animal has no ledger).</param>
/// <param name="GrossProfitBdt">Realised P&amp;L: realised revenue − full cost. Understates the batch while animals are still on hand.</param>
/// <param name="ReturnOnInvestmentPercent">Realised ROI as a 0–1 fraction (GrossProfit / TotalCost).</param>
/// <param name="TotalAnimals">Every animal in the batch, whether or not it has a cost ledger.</param>
/// <param name="OnHandAnimals">Active or quarantined animals still on the farm.</param>
/// <param name="UnrealisedValueBdt">Value of the on-hand animals under the farm's valuation policy.</param>
/// <param name="EstimatedTotalProfitBdt">Realised revenue + unrealised value − full cost.</param>
/// <param name="EstimatedRoiPercent">Estimated ROI as a 0–1 fraction (EstimatedTotalProfit / TotalCost).</param>
/// <param name="UnweighedAtCostAnimals">On-hand animals with no weight record yet, valued at their purchase cost instead of by weight.</param>
/// <param name="UnweighedAtCostValueBdt">The part of <paramref name="UnrealisedValueBdt"/> that is those animals' purchase cost.</param>
public record BatchPnLReportDto(
    Guid BatchId,
    Guid FarmId,
    decimal TotalIncomeBdt,
    decimal TotalCostBdt,
    decimal GrossProfitBdt,
    decimal ReturnOnInvestmentPercent,
    int TotalAnimals,
    int OnHandAnimals = 0,
    decimal UnrealisedValueBdt = 0m,
    decimal EstimatedTotalProfitBdt = 0m,
    decimal EstimatedRoiPercent = 0m,
    int UnweighedAtCostAnimals = 0,
    decimal UnweighedAtCostValueBdt = 0m
);
