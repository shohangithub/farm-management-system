using System;
using System.Collections.Generic;

namespace Farm360.Contracts.Finance;

public record PagedFinancialTransactionsResult(
    IReadOnlyList<FinancialTransactionDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize,
    decimal TotalIncomeBdt,
    decimal TotalExpenseBdt,
    decimal NetCashFlowBdt
)
{
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber * PageSize < TotalCount;
}
