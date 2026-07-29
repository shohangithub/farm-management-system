using Farm360.Application.Common.Models;
using Farm360.Application.Inventory.DTOs;
using Farm360.Application.Inventory.Mappings;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Inventory.Queries.StockTransactions;

public sealed record GetStockTransactionsQuery(
    int PageNumber = 1,
    int PageSize = 20,
    Guid? FarmId = null,
    Guid? InventoryItemId = null,
    StockTransactionType? TransactionType = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    string? Search = null,
    string? SortBy = null,
    bool SortDesc = false) : IRequest<PagedResult<StockTransactionDto>>;

public sealed class GetStockTransactionsQueryHandler : IRequestHandler<GetStockTransactionsQuery, PagedResult<StockTransactionDto>>
{
    private readonly IStockTransactionRepository _transactionRepository;

    public GetStockTransactionsQueryHandler(IStockTransactionRepository transactionRepository)
    {
        _transactionRepository = transactionRepository;
    }

    public async Task<PagedResult<StockTransactionDto>> Handle(GetStockTransactionsQuery request, CancellationToken cancellationToken)
    {
        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var (transactions, count) = await _transactionRepository.GetPagedAsync(
            pageNumber,
            pageSize,
            request.FarmId,
            request.InventoryItemId,
            request.TransactionType,
            request.FromDate,
            request.ToDate,
            request.Search,
            request.SortBy,
            request.SortDesc,
            cancellationToken);

        var dtos = transactions
            .Select(t => t.Transaction.ToDto(t.ItemName, t.SupplierName))
            .ToList();

        return new PagedResult<StockTransactionDto>(dtos, count, pageNumber, pageSize);
    }
}
