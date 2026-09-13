using System;

namespace Farm360.Contracts.Finance;

public record FarmShareConfigDto(
    Guid Id,
    Guid FarmId,
    int TotalShares,
    decimal SharePriceBdt,
    int OwnerShareCount,
    int AllocatedShareCount,
    int AvailableShareCount,
    decimal TotalValuationBdt,
    decimal AvailableValuationBdt,
    decimal OwnerEquityValueBdt,
    decimal OwnerOwnershipPercentage,
    int MinimumPurchaseShares,
    bool IsShareSaleOpen,
    DateTime LastValuationDate,
    string? ValuationNotes
);
