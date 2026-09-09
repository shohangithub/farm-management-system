namespace Farm360.Contracts.Finance;

public record UpdateInvestorRequest(
    string Name,
    decimal? AgreedProfitSharePercentage = null,
    string? Email = null,
    string? Phone = null,
    string? NationalId = null,
    string? Notes = null
);
