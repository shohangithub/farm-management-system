using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Contracts.Intelligence;
using Farm360.Domain.Intelligence.Projections;
using MediatR;

namespace Farm360.Application.Intelligence.Queries.CalculateProfitProjection;

public sealed record CalculateProfitProjectionQuery(
    Guid? AnimalId,
    FatteningProjectionInputsDto Inputs,
    bool IncludeDailyRows) : IRequest<ProfitProjectionResponse>;

public sealed class CalculateProfitProjectionQueryHandler : IRequestHandler<CalculateProfitProjectionQuery, ProfitProjectionResponse>
{
    public Task<ProfitProjectionResponse> Handle(CalculateProfitProjectionQuery request, CancellationToken cancellationToken)
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

        var result = FatteningProjectionCalculator.Calculate(domainInputs);

        var summaryDto = new FatteningProjectionSummaryDto(
            Math.Round(result.Summary.StartingWeightKg, 3),
            Math.Round(result.Summary.FinalWeightKg, 3),
            Math.Round(result.Summary.TotalGainKg, 3),
            Math.Round(result.Summary.PurchaseCostBdt, 2),
            Math.Round(result.Summary.TotalFeedCostBdt, 2),
            Math.Round(result.Summary.TotalGrassCostBdt, 2),
            Math.Round(result.Summary.TotalOtherCostBdt, 2),
            Math.Round(result.Summary.TotalLaborCostBdt, 2),
            Math.Round(result.Summary.TotalFarmingCostBdt, 2),
            Math.Round(result.Summary.TotalInvestmentBdt, 2),
            Math.Round(result.Summary.FinalMeatWeightKg, 3),
            Math.Round(result.Summary.ExpectedSaleValueBdt, 2),
            Math.Round(result.Summary.ProfitLossBdt, 2),
            Math.Round(result.Summary.ProfitPercent, 4),
            Math.Round(result.Summary.BreakEvenPricePerLiveKgBdt, 2),
            Math.Round(result.Summary.BreakEvenPricePerMeatKgBdt, 2),
            result.Summary.BreakEvenDay,
            result.Summary.OptimalSaleDay,
            Math.Round(result.Summary.OptimalProfitBdt, 2),
            Math.Round(result.Summary.TotalFeedQtyKg, 3),
            Math.Round(result.Summary.CostPerKgGainBdt, 2),
            Math.Round(result.Summary.RoiPercent, 4),
            Math.Round(result.Summary.DailyProfitRateBdt, 2),
            Math.Round(result.Summary.MeatPriceUsedBdtPerKg, 2)
        );

        var daysDto = request.IncludeDailyRows ? result.Days.Select(d => new FatteningProjectionDayDto(
            d.Day,
            Math.Round(d.LiveWeightKg, 3),
            Math.Round(d.MeatWeightKg, 3),
            Math.Round(d.FeedQtyKg, 3),
            Math.Round(d.FeedCostBdt, 2),
            Math.Round(d.GrassCostBdt, 2),
            Math.Round(d.OtherCostBdt, 2),
            Math.Round(d.LaborCostBdt, 2),
            Math.Round(d.DailyTotalCostBdt, 2),
            Math.Round(d.MeatGainKg, 3),
            Math.Round(d.MeatValueBdt, 2),
            Math.Round(d.CumulativeCostBdt, 2),
            Math.Round(d.TotalInvestmentBdt, 2),
            Math.Round(d.ProfitLossBdt, 2),
            Math.Round(d.ProfitPercent, 4)
        )).ToList() : new System.Collections.Generic.List<FatteningProjectionDayDto>();

        var response = new ProfitProjectionResponse(request.Inputs, summaryDto, daysDto);
        return Task.FromResult(response);
    }
}
