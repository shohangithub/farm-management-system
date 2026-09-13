using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance.Interfaces;
using MediatR;

namespace Farm360.Application.Finance.Queries;

public record GetShareMarketOverviewQuery(Guid FarmId) : IRequest<ShareMarketOverviewDto>;

public class GetShareMarketOverviewQueryHandler : IRequestHandler<GetShareMarketOverviewQuery, ShareMarketOverviewDto>
{
    private static readonly string[] SliceColors = [
        "#0d9488", // Teal
        "#10b981", // Emerald
        "#6366f1", // Indigo
        "#8b5cf6", // Violet
        "#f59e0b", // Amber
        "#0284c7", // Sky
        "#ec4899", // Pink
        "#f97316", // Orange
        "#14b8a6", // Mint
        "#64748b"  // Slate
    ];

    private readonly IFarmShareRepository _shareRepository;
    private readonly IInvestorRepository _investorRepository;

    public GetShareMarketOverviewQueryHandler(
        IFarmShareRepository shareRepository,
        IInvestorRepository investorRepository)
    {
        _shareRepository = shareRepository;
        _investorRepository = investorRepository;
    }

    public async Task<ShareMarketOverviewDto> Handle(GetShareMarketOverviewQuery request, CancellationToken cancellationToken)
    {
        var config = await _shareRepository.GetConfigByFarmIdAsync(request.FarmId, cancellationToken);

        if (config == null)
        {
            return new ShareMarketOverviewDto(
                request.FarmId,
                false,
                null,
                0,
                0,
                0,
                [],
                [],
                []
            );
        }

        var configDto = new FarmShareConfigDto(
            config.Id,
            config.FarmId,
            config.TotalShares,
            config.SharePriceBdt,
            config.OwnerShareCount,
            config.AllocatedShareCount,
            config.AvailableShareCount,
            config.TotalValuationBdt,
            config.AvailableValuationBdt,
            config.OwnerEquityValueBdt,
            config.OwnerOwnershipPercentage,
            config.MinimumPurchaseShares,
            config.IsShareSaleOpen,
            config.LastValuationDate,
            config.ValuationNotes
        );

        var holdings = await _shareRepository.GetHoldingsByFarmIdAsync(request.FarmId, cancellationToken);
        var investors = await _investorRepository.GetByFarmIdAsync(request.FarmId, false, cancellationToken);
        var investorMap = investors.ToDictionary(i => i.Id);

        var shareholderDtos = new List<ShareHoldingDto>();
        foreach (var h in holdings.Where(h => h.ShareCount > 0))
        {
            investorMap.TryGetValue(h.InvestorId, out var inv);
            var invName = inv?.Name ?? "Unknown Investor";
            var currentValue = h.ShareCount * config.SharePriceBdt;
            var gainLoss = currentValue - h.TotalInvestedBdt;
            var roi = h.TotalInvestedBdt > 0
                ? Math.Round((gainLoss / h.TotalInvestedBdt) * 100m, 2)
                : 0m;
            var ownershipPct = config.TotalShares > 0
                ? Math.Round(((decimal)h.ShareCount / config.TotalShares) * 100m, 2)
                : 0m;

            shareholderDtos.Add(new ShareHoldingDto(
                h.Id,
                h.InvestorId,
                invName,
                inv?.Phone,
                inv?.Email,
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

        // Build distribution slices
        var distribution = new List<ShareDistributionSliceDto>();

        // 1. Owner slice
        if (config.OwnerShareCount > 0)
        {
            distribution.Add(new ShareDistributionSliceDto(
                "Farm Owner (Retained)",
                config.OwnerShareCount,
                config.OwnerOwnershipPercentage,
                config.OwnerEquityValueBdt,
                "#0f766e", // Deep Teal
                true,
                false
            ));
        }

        // 2. Individual shareholders
        int colorIdx = 1;
        foreach (var sh in shareholderDtos)
        {
            var color = SliceColors[colorIdx % SliceColors.Length];
            colorIdx++;

            distribution.Add(new ShareDistributionSliceDto(
                sh.InvestorName,
                sh.ShareCount,
                sh.OwnershipPercentage,
                sh.CurrentValueBdt,
                color,
                false,
                false
            ));
        }

        // 3. Available shares pool
        if (config.AvailableShareCount > 0)
        {
            var availPct = config.TotalShares > 0
                ? Math.Round(((decimal)config.AvailableShareCount / config.TotalShares) * 100m, 2)
                : 0m;

            distribution.Add(new ShareDistributionSliceDto(
                "Available for Purchase",
                config.AvailableShareCount,
                availPct,
                config.AvailableValuationBdt,
                "#94a3b8", // Slate
                false,
                true
            ));
        }

        // Recent transactions
        var rawTxs = await _shareRepository.GetTransactionsByFarmIdAsync(request.FarmId, cancellationToken);
        var recentTxDtos = rawTxs.Take(25).Select(t =>
        {
            investorMap.TryGetValue(t.InvestorId, out var inv);
            string? cpName = null;
            if (t.CounterpartyInvestorId.HasValue && investorMap.TryGetValue(t.CounterpartyInvestorId.Value, out var cp))
            {
                cpName = cp.Name;
            }

            return new ShareTransactionDto(
                t.Id,
                t.FarmId,
                t.InvestorId,
                inv?.Name ?? "Unknown Investor",
                t.Type.ToString(),
                t.ShareCount,
                t.PricePerShareBdt,
                t.TotalAmountBdt,
                t.TransactionDate,
                t.CounterpartyInvestorId,
                cpName,
                t.ReferenceId,
                t.Notes,
                t.FinancialTransactionId,
                t.CreatedAtUtc
            );
        }).ToList();

        var totalCapitalRaised = shareholderDtos.Sum(s => s.TotalInvestedBdt);

        return new ShareMarketOverviewDto(
            request.FarmId,
            true,
            configDto,
            shareholderDtos.Count,
            config.AllocatedShareCount,
            totalCapitalRaised,
            distribution,
            shareholderDtos,
            recentTxDtos
        );
    }
}
