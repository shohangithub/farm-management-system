using MediatR;
using System;

namespace Farm360.Application.Features.Intelligence.Queries.GetAnimalFinancialSnapshot;

public sealed record GetAnimalFinancialSnapshotQuery(Guid AnimalId) : IRequest<AnimalFinancialSnapshotDto>;

/// <param name="TotalInvestmentBdt">Cost to date (acquisition + feed + vet + labour + overhead).</param>
/// <param name="Projected30DayCostBdt">
/// Projected running cost over the next 30 days — feed, grass, other and labour, not feed alone.
/// </param>
/// <param name="Projected60DayCostBdt">Projected running cost over the next 60 days.</param>
/// <param name="EstimatedMarketValueBdt">Today's value under the farm's valuation policy.</param>
/// <param name="CurrentProfitMarginBdt">Estimated value minus cost to date.</param>
public sealed record AnimalFinancialSnapshotDto(
    Guid AnimalId,
    decimal TotalInvestmentBdt,
    decimal Projected30DayCostBdt,
    decimal Projected60DayCostBdt,
    decimal EstimatedMarketValueBdt,
    decimal CurrentProfitMarginBdt);
