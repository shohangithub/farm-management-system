namespace Farm360.Contracts.Finance;

public record ConfigureFarmSharesRequest(
    int TotalShares,
    decimal SharePriceBdt,
    int OwnerShareCount,
    int MinimumPurchaseShares = 1,
    bool IsShareSaleOpen = true,
    string? ValuationNotes = null
);
