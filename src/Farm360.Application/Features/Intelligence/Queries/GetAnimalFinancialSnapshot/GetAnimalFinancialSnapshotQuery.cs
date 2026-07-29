using Farm360.Domain.Intelligence.ValueObjects;
using MediatR;
using System;

namespace Farm360.Application.Features.Intelligence.Queries.GetAnimalFinancialSnapshot;

public sealed record GetAnimalFinancialSnapshotQuery(Guid AnimalId) : IRequest<AnimalFinancialSnapshotDto>;

public sealed record AnimalFinancialSnapshotDto(
    Guid AnimalId,
    decimal TotalInvestmentBdt,
    decimal Projected30DayFeedCostBdt,
    decimal Projected60DayFeedCostBdt,
    decimal EstimatedMarketValueBdt,
    decimal CurrentProfitMarginBdt);
