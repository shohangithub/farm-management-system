namespace Farm360.Contracts.Finance;

public record UpdateShareValuationRequest(
    decimal NewSharePriceBdt,
    string? ValuationNotes = null
);
