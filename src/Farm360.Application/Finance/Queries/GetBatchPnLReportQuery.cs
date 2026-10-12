using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Repositories;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Application.Finance.Services;
using MediatR;

namespace Farm360.Application.Finance.Queries;

public record GetBatchPnLReportQuery(Guid FarmId, Guid BatchId) : IRequest<BatchPnLReportDto>;

public class GetBatchPnLReportQueryHandler : IRequestHandler<GetBatchPnLReportQuery, BatchPnLReportDto>
{
    private readonly IFinancialTransactionRepository _transactionRepository;
    private readonly IAnimalCostLedgerRepository _ledgerRepository;

    public GetBatchPnLReportQueryHandler(
        IFinancialTransactionRepository transactionRepository,
        IAnimalCostLedgerRepository ledgerRepository)
    {
        _transactionRepository = transactionRepository;
        _ledgerRepository = ledgerRepository;
    }

    public async Task<BatchPnLReportDto> Handle(GetBatchPnLReportQuery request, CancellationToken cancellationToken)
    {
        // 1. Costs: the cost ledgers of the batch's animals (purchase, feed, health, labour, overhead).
        var ledgers = await _ledgerRepository.GetByBatchIdAsync(request.FarmId, request.BatchId, cancellationToken);
        var batchAnimals = ledgers.Select(l => l.AnimalId).ToHashSet();

        // 2. Revenue: sales and other revenue booked to the batch or to any of its animals. Only
        //    P&L revenue counts — investor capital or a loan tagged to a batch is not batch income,
        //    and animal sales (posted per animal) were previously missed entirely.
        var transactions = await _transactionRepository.GetAllByFarmIdAsync(request.FarmId, cancellationToken);
        var totalIncome = AccountChart.Revenue(transactions.Where(t =>
            t.BatchId == request.BatchId || (t.AnimalId.HasValue && batchAnimals.Contains(t.AnimalId.Value))));
        var totalCost = ledgers.Sum(l => l.TotalCostBdt);
        var totalAnimals = ledgers.Count;

        // 3. Calculate ROI
        var grossProfit = totalIncome - totalCost;
        var roiPercent = totalCost > 0 ? (grossProfit / totalCost) * 100 : 0m;

        return new BatchPnLReportDto(
            request.BatchId,
            request.FarmId,
            Math.Round(totalIncome, 2),
            Math.Round(totalCost, 2),
            Math.Round(grossProfit, 2),
            Math.Round(roiPercent, 2),
            totalAnimals
        );
    }
}
