using System;
using System.Collections.Generic;

namespace Farm360.Contracts.Finance;

/// <summary>
/// Credit terms for a purchase or sale that is not (fully) paid on the day. Leave it out for a cash deal.
/// </summary>
/// <param name="PartyName">Supplier or customer the balance is owed to / by.</param>
/// <param name="DueDate">When it should be settled. Defaults to 30 days after the deal.</param>
/// <param name="PaidNowBdt">Any part paid on the day (an advance or part payment).</param>
public record CreditTermsRequest(
    string PartyName,
    string? PartyPhone = null,
    DateOnly? DueDate = null,
    decimal PaidNowBdt = 0m,
    string? PaymentMethod = null,
    string? Notes = null
);

/// <summary>Puts an existing cash-booked purchase or sale on credit.</summary>
public record OpenTradeBillRequest(
    Guid TransactionId,
    string PartyName,
    string? PartyPhone = null,
    DateOnly? DueDate = null,
    decimal PaidNowBdt = 0m,
    string? PaymentMethod = null,
    string? Notes = null
);

public record RecordBillPaymentRequest(
    decimal AmountBdt,
    DateOnly PaymentDate,
    string? Method = null,
    string? Reference = null,
    string? Notes = null
);

/// <summary>
/// Clears the rest of a bill without cash: a customer debt that will not be collected (bad debt),
/// or a balance the supplier has waived.
/// </summary>
public record WriteOffBillRequest(DateOnly Date, string? Reason = null, decimal? AmountBdt = null);

public record UpdateBillTermsRequest(string PartyName, string? PartyPhone, DateOnly? DueDate, string? Notes);

public record TradeBillPaymentDto(
    Guid Id,
    Guid BillId,
    DateOnly PaymentDate,
    decimal AmountBdt,
    string Method,
    string? Reference,
    string? Notes
);

public record TradeBillDto(
    Guid Id,
    Guid FarmId,
    string Kind,
    string Status,
    string PartyName,
    string? PartyPhone,
    Guid? SupplierId,
    Guid SourceDocumentId,
    string Category,
    string CategoryName,
    string? SourceModule,
    string ReferenceNo,
    string Description,
    string? Notes,
    DateOnly BillDate,
    DateOnly? DueDate,
    int DaysOverdue,
    string AgingBucket,
    decimal AmountBdt,
    decimal PaidBdt,
    decimal AdjustedBdt,
    decimal OutstandingBdt,
    IReadOnlyList<TradeBillPaymentDto> Payments
);

public record AgingBucketDto(string Key, string Label, int Count, decimal AmountBdt);

public record PartyBalanceDto(
    string PartyName,
    string? PartyPhone,
    int OpenBills,
    decimal BilledBdt,
    decimal PaidBdt,
    decimal OutstandingBdt,
    decimal OverdueBdt,
    DateOnly? OldestDueDate
);

/// <summary>One side of the credit book — what the farm owes (payables) or is owed (receivables).</summary>
public record TradeCreditSideDto(
    string Kind,
    decimal OutstandingBdt,
    decimal OverdueBdt,
    decimal DueThisWeekBdt,
    int OpenBills,
    decimal LedgerBalanceBdt,
    bool IsReconciled,
    IReadOnlyList<AgingBucketDto> Aging,
    IReadOnlyList<PartyBalanceDto> Parties
);

public record TradeCreditOverviewDto(
    Guid FarmId,
    DateOnly AsOf,
    TradeCreditSideDto Payables,
    TradeCreditSideDto Receivables,
    IReadOnlyList<TradeBillDto> Bills
);

/// <summary>Credit movement in a report period (full accounts report).</summary>
public record TradeCreditMovementDto(
    decimal ReceivablesOpeningBdt,
    decimal CreditSalesBdt,
    decimal CollectedBdt,
    decimal BadDebtsBdt,
    decimal ReceivablesClosingBdt,
    decimal PayablesOpeningBdt,
    decimal CreditPurchasesBdt,
    decimal PaidToSuppliersBdt,
    decimal ReturnsAndWaiversBdt,
    decimal PayablesClosingBdt
);
