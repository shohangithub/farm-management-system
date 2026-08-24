using System;

namespace Farm360.Domain.Intelligence.Projections;

public static class FatteningProjectionValidator
{
    public static void Validate(FatteningProjectionInputs inputs)
    {
        if (inputs.StartingLiveWeightKg <= 0)
            throw new ArgumentException("Starting weight must be greater than zero.", nameof(inputs));
            
        if (inputs.FatteningPeriodDays < 1 || inputs.FatteningPeriodDays > 1095)
            throw new ArgumentException("Fattening period must be between 1 and 1095 days.", nameof(inputs));
            
        if (inputs.InitialMeatYieldRatio < 0 || inputs.InitialMeatYieldRatio > 1)
            throw new ArgumentException("Meat yield ratio must be between 0 and 1.", nameof(inputs));
            
        if (inputs.MeatYieldOnDailyGainRatio < 0 || inputs.MeatYieldOnDailyGainRatio > 1)
            throw new ArgumentException("Meat yield on gain ratio must be between 0 and 1.", nameof(inputs));
            
        if (inputs.PurchasePriceBdt < 0)
            throw new ArgumentException("Purchase price cannot be negative.", nameof(inputs));
            
        if (inputs.CurrentMeatPriceBdtPerKg < 0)
            throw new ArgumentException("Meat price cannot be negative.", nameof(inputs));
            
        if (inputs.DailyLiveWeightGainKg < 0)
            throw new ArgumentException("Daily live weight gain cannot be negative.", nameof(inputs));
            
        if (inputs.DailyFeedQuantityKgAtStart < 0)
            throw new ArgumentException("Feed quantity cannot be negative.", nameof(inputs));
            
        if (inputs.FeedPriceBdtPerKg < 0)
            throw new ArgumentException("Feed price cannot be negative.", nameof(inputs));
            
        if (inputs.DailyGrassCostBdt < 0)
            throw new ArgumentException("Grass cost cannot be negative.", nameof(inputs));
            
        if (inputs.DailyOtherCostBdt < 0)
            throw new ArgumentException("Other cost cannot be negative.", nameof(inputs));
            
        if (inputs.MonthlyLaborCostBdt < 0)
            throw new ArgumentException("Labor cost cannot be negative.", nameof(inputs));
    }
}
