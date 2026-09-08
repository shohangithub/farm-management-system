using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Repositories;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance.Enums;
using MediatR;

namespace Farm360.Application.Finance.Queries;

public sealed record GetPagedFinancialTransactionsQuery(
    Guid FarmId,
    int PageNumber = 1,
    int PageSize = 20,
    string? Search = null,
    TransactionType? Type = null,
    TransactionCategory? Category = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    Guid? AnimalId = null,
    Guid? BatchId = null,
    string? SortBy = null,
    bool SortDesc = true
) : IRequest<PagedFinancialTransactionsResult>;

public sealed class GetPagedFinancialTransactionsQueryHandler : IRequestHandler<GetPagedFinancialTransactionsQuery, PagedFinancialTransactionsResult>
{
    private readonly IFinancialTransactionRepository _repository;

    public GetPagedFinancialTransactionsQueryHandler(IFinancialTransactionRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedFinancialTransactionsResult> Handle(GetPagedFinancialTransactionsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount, totalIncome, totalExpense) = await _repository.GetPagedAsync(
            request.FarmId,
            request.PageNumber,
            request.PageSize,
            request.Search,
            request.Type,
            request.Category,
            request.StartDate,
            request.EndDate,
            request.AnimalId,
            request.BatchId,
            request.SortBy,
            request.SortDesc,
            cancellationToken
        );

        var dtos = items.Select(t => new FinancialTransactionDto(
            t.Id,
            t.FarmId,
            t.Type.ToString(),
            t.Category.ToString(),
            t.AmountBdt,
            t.TransactionDate,
            t.Description,
            t.ReferenceId,
            t.Notes,
            t.AnimalId,
            t.BatchId,
            t.ShedId,
            t.CreatedAtUtc
        )).ToList();

        var netCashFlow = totalIncome - totalExpense;

        return new PagedFinancialTransactionsResult(
            dtos,
            totalCount,
            request.PageNumber,
            request.PageSize,
            totalIncome,
            totalExpense,
            netCashFlow
        );
    }
}
