using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Contracts.Intelligence;
using Farm360.Domain.Intelligence.Projections;
using MediatR;

namespace Farm360.Application.Intelligence.Queries.SolveBreakEven;

public sealed record SolveBreakEvenQuery(
    FatteningProjectionInputsDto Inputs,
    BreakEvenTarget Target) : IRequest<decimal>;

public sealed class SolveBreakEvenQueryHandler : IRequestHandler<SolveBreakEvenQuery, decimal>
{
    public Task<decimal> Handle(SolveBreakEvenQuery request, CancellationToken cancellationToken)
    {
        var domainInputs = new FatteningProjectionInputs(
            request.Inputs.StartingLiveWeightKg,
            request.Inputs.PurchasePriceBdt,
            request.Inputs.CurrentMeatPriceBdtPerKg,
            request.Inputs.InitialMeatYieldRatio,
            request.Inputs.DailyLiveWeightGainKg,
            request.Inputs.MeatYieldOnDailyGainRatio,
            request.Inputs.DailyFeedQuantityKgAtStart,
            request.Inputs.FeedPriceBdtPerKg,
            request.Inputs.DailyGrassCostBdt,
            request.Inputs.DailyOtherCostBdt,
            request.Inputs.MonthlyLaborCostBdt,
            request.Inputs.FatteningPeriodDays
        );

        decimal result = 0;
        
        switch (request.Target)
        {
            case BreakEvenTarget.RequiredMeatPrice:
                result = FatteningProjectionCalculator.SolveForRequiredMeatPrice(domainInputs);
                break;
            case BreakEvenTarget.MaximumPurchasePrice:
            case BreakEvenTarget.RequiredAdg:
                // Not implemented in the domain calculator yet. Returning 0 here used to read as a
                // real answer ("maximum purchase price: 0 BDT"), so fail explicitly instead.
                throw new InvalidOperationException($"Solving for '{request.Target}' is not supported yet.");
            default:
                throw new ArgumentOutOfRangeException(nameof(request), "Unsupported target");
        }

        return Task.FromResult(Math.Round(result, 2));
    }
}
