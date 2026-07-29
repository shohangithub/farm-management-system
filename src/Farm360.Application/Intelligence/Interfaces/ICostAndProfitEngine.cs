using System;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Application.Intelligence.Interfaces;

public record CostAndProfitSnapshot(
    decimal Projected30DayFeedCostBdt,
    decimal TotalInvestmentBdt
);

public interface ICostAndProfitEngine
{
    Task<CostAndProfitSnapshot?> CalculateSnapshotAsync(Guid animalId, CancellationToken cancellationToken = default);
}
