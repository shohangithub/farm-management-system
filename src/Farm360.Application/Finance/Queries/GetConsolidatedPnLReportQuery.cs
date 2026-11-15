using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Repositories;
using Farm360.Contracts.Finance;
using Farm360.Domain.Farms.Repositories;
using Farm360.Application.Finance.Services;
using MediatR;

namespace Farm360.Application.Finance.Queries;

public record GetConsolidatedPnLReportQuery(Guid TenantId, int Year, int Month) : IRequest<ConsolidatedPnLReportDto>;

/// <remarks>
/// Each farm's profit comes from <see cref="IFinancialStatementEngine"/> — the same lines as its
/// income statement, so depreciation, disposals and year-end adjustments are included.
/// </remarks>
public class GetConsolidatedPnLReportQueryHandler : IRequestHandler<GetConsolidatedPnLReportQuery, ConsolidatedPnLReportDto>
{
    private readonly IFinancialTransactionRepository _repository;
    private readonly IFinancialStatementEngine _engine;
    private readonly IFarmRepository _farms;

    public GetConsolidatedPnLReportQueryHandler(
        IFinancialTransactionRepository repository,
        IFinancialStatementEngine engine,
        IFarmRepository farms)
    {
        _repository = repository;
        _engine = engine;
        _farms = farms;
    }

    public async Task<ConsolidatedPnLReportDto> Handle(GetConsolidatedPnLReportQuery request, CancellationToken cancellationToken)
    {
        var from = new DateOnly(request.Year, request.Month, 1);
        var to = from.AddMonths(1).AddDays(-1);

        // Every farm of the tenant (a farm with only fixed assets still has depreciation), plus any
        // farm id that only appears on postings.
        var farms = await _farms.GetAllByTenantAsync(request.TenantId, cancellationToken);
        var postings = await _repository.GetAllByTenantIdAsync(request.TenantId, cancellationToken);
        var farmIds = farms.Select(f => f.Id)
            .Concat(postings.Select(t => t.FarmId))
            .Distinct()
            .ToList();

        var farmBreakdown = new Dictionary<Guid, FarmPnLSnapshotDto>();
        foreach (var farmId in farmIds)
        {
            var ledger = await _engine.LoadAsync(farmId, to, cancellationToken);
            var revenue = ledger.RevenueBetween(from, to);
            var expenses = ledger.ExpensesBetween(from, to);
            if (revenue == 0m && expenses == 0m)
                continue;

            farmBreakdown[farmId] = new FarmPnLSnapshotDto(
                farmId,
                Math.Round(revenue, 2),
                Math.Round(expenses, 2),
                Math.Round(revenue - expenses, 2));
        }

        var totalIncome = farmBreakdown.Values.Sum(f => f.TotalIncomeBdt);
        var totalExpense = farmBreakdown.Values.Sum(f => f.TotalExpenseBdt);

        return new ConsolidatedPnLReportDto(
            request.Year,
            request.Month,
            Math.Round(totalIncome, 2),
            Math.Round(totalExpense, 2),
            Math.Round(totalIncome - totalExpense, 2),
            farmBreakdown
        );
    }
}
