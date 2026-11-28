using System;
using System.Collections.Generic;

namespace Farm360.Contracts.Finance;

/// <summary>A place the farm keeps money (cash in hand, bank, mobile wallet) and its balance.</summary>
public record MoneyAccountDto(
    Guid Id,
    Guid FarmId,
    string Name,
    string Kind,
    string? ProviderName,
    string? AccountNumber,
    string? Branch,
    string? Notes,
    bool IsDefault,
    bool IsActive,
    decimal BalanceBdt);

/// <param name="Kind">Cash, Bank or MobileWallet.</param>
public record SaveMoneyAccountRequest(
    string Name,
    string Kind,
    string? ProviderName = null,
    string? AccountNumber = null,
    string? Branch = null,
    string? Notes = null);

/// <summary>Moves money between two of the farm's own accounts (e.g. bank → cash in hand). Not income or expense.</summary>
public record TransferBetweenAccountsRequest(
    Guid FromAccountId,
    Guid ToAccountId,
    decimal AmountBdt,
    DateTime TransferDate,
    string? ReferenceId = null,
    string? Notes = null);

/// <summary>The two legs of a transfer.</summary>
public record MoneyTransferDto(
    Guid OutTransactionId,
    Guid InTransactionId,
    Guid FromAccountId,
    Guid ToAccountId,
    decimal AmountBdt,
    DateTime TransferDate,
    string ReferenceId);

/// <summary>One line of an account's cash book.</summary>
/// <param name="CounterAccountName">For a transfer leg: the other account.</param>
public record MoneyAccountLineDto(
    Guid TransactionId,
    DateTime Date,
    string VoucherNo,
    string Description,
    string Category,
    string CategoryName,
    string CategoryNameBn,
    decimal InBdt,
    decimal OutBdt,
    decimal BalanceBdt,
    bool IsTransfer,
    string? CounterAccountName,
    Guid? LinkedTransactionId,
    string? SourceModule);

/// <summary>A money account's statement for a period with its running balance.</summary>
public record MoneyAccountStatementDto(
    MoneyAccountDto Account,
    DateOnly FromDate,
    DateOnly ToDate,
    decimal OpeningBdt,
    decimal TotalInBdt,
    decimal TotalOutBdt,
    decimal ClosingBdt,
    IReadOnlyList<MoneyAccountLineDto> Lines);

/// <summary>Cash &amp; bank split by money account (dashboard and full accounts report).</summary>
public record MoneyAccountBreakdownDto(
    Guid Id,
    string Name,
    string Kind,
    bool IsDefault,
    decimal BalanceBdt);
