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
                // TODO: Implement Maximum Purchase Price solving in the Domain calculator (Phase 1 or 2.1)
                break;
            case BreakEvenTarget.RequiredAdg:
                // TODO: Implement Required ADG solving in the Domain calculator (Phase 1 or 2.1)
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(request), "Unsupported target");
        }

        return Task.FromResult(Math.Round(result, 2));
    }
}
