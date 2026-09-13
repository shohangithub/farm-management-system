using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance.Interfaces;
using MediatR;

namespace Farm360.Application.Finance.Queries;

public record GetShareHoldingsByInvestorQuery(Guid InvestorId) : IRequest<IReadOnlyList<ShareHoldingDto>>;

public class GetShareHoldingsByInvestorQueryHandler : IRequestHandler<GetShareHoldingsByInvestorQuery, IReadOnlyList<ShareHoldingDto>>
{
    private readonly IFarmShareRepository _shareRepository;
    private readonly IInvestorRepository _investorRepository;

    public GetShareHoldingsByInvestorQueryHandler(
        IFarmShareRepository shareRepository,
        IInvestorRepository investorRepository)
    {
        _shareRepository = shareRepository;
        _investorRepository = investorRepository;
    }

    public async Task<IReadOnlyList<ShareHoldingDto>> Handle(GetShareHoldingsByInvestorQuery request, CancellationToken cancellationToken)
    {
        var investor = await _investorRepository.GetByIdAsync(request.InvestorId, cancellationToken);
        if (investor == null)
        {
            return [];
        }

        var holdings = await _shareRepository.GetHoldingsByInvestorIdAsync(request.InvestorId, cancellationToken);
        var result = new List<ShareHoldingDto>();

        foreach (var h in holdings)
        {
            var config = await _shareRepository.GetConfigByFarmIdAsync(h.FarmId, cancellationToken);
            var sharePrice = config?.SharePriceBdt ?? h.AveragePurchasePriceBdt;
            var totalShares = config?.TotalShares ?? 0;

            var currentValue = h.ShareCount * sharePrice;
            var gainLoss = currentValue - h.TotalInvestedBdt;
            var roi = h.TotalInvestedBdt > 0
                ? Math.Round((gainLoss / h.TotalInvestedBdt) * 100m, 2)
                : 0m;
            var ownershipPct = totalShares > 0
                ? Math.Round(((decimal)h.ShareCount / totalShares) * 100m, 2)
                : 0m;

            result.Add(new ShareHoldingDto(
                h.Id,
                h.InvestorId,
                investor.Name,
                investor.Phone,
                investor.Email,
                h.FarmId,
                h.ShareCount,
                h.AveragePurchasePriceBdt,
                h.TotalInvestedBdt,
                currentValue,
                ownershipPct,
                gainLoss,
                roi,
                h.CertificateNumber,
                h.Notes,
                h.IsActive
            ));
        }

        return result;
    }
}
