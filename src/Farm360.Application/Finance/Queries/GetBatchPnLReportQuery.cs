using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Repositories;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance.Enums;
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
        // 1. Get all transactions for this batch (Income)
        var transactions = await _transactionRepository.GetAllByBatchIdAsync(request.BatchId, cancellationToken);
        var totalIncome = transactions.Where(t => t.Type == TransactionType.Income).Sum(t => t.AmountBdt);

        // 2. Get all cost ledgers for animals in this batch (Costs)
        var ledgers = await _ledgerRepository.GetByBatchIdAsync(request.FarmId, request.BatchId, cancellationToken);
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
