using System;

namespace Farm360.Contracts.Finance;

public record ShareHoldingDto(
    Guid Id,
    Guid InvestorId,
    string InvestorName,
    string? InvestorPhone,
    string? InvestorEmail,
    Guid FarmId,
    int ShareCount,
    decimal AveragePurchasePriceBdt,
    decimal TotalInvestedBdt,
    decimal CurrentValueBdt,
    decimal OwnershipPercentage,
    decimal UnrealizedGainLossBdt,
    decimal ReturnOnInvestmentPercent,
    string? CertificateNumber,
    string? Notes,
    bool IsActive
);
