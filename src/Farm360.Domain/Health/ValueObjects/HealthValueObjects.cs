using Farm360.Domain.Common;

namespace Farm360.Domain.Health.ValueObjects;

/// <summary>
/// Value Object representing medicine dosage.
/// </summary>
public sealed class Dosage : BaseValueObject
{
    private Dosage() { } // EF Core

    private Dosage(decimal amount, string unit)
    {
        Amount = amount;
        Unit = unit;
    }

    public decimal Amount { get; private set; }
    public string Unit { get; private set; } = string.Empty;

    public static Dosage Create(decimal amount, string unit)
    {
        if (amount <= 0)
            throw new ArgumentException("Dosage amount must be greater than zero.", nameof(amount));

        if (string.IsNullOrWhiteSpace(unit))
            throw new ArgumentException("Dosage unit is required.", nameof(unit));

        return new Dosage(Math.Round(amount, 2), unit.Trim());
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Unit.ToLowerInvariant();
    }

    public override string ToString() => $"{Amount:F2} {Unit}";
}

/// <summary>
/// Value Object representing milk and meat withdrawal periods in days.
/// </summary>
public sealed class WithdrawalPeriod : BaseValueObject
{
    private WithdrawalPeriod() { } // EF Core

    private WithdrawalPeriod(int milkDays, int meatDays)
    {
        MilkDays = milkDays;
        MeatDays = meatDays;
    }

    public int MilkDays { get; private set; }
    public int MeatDays { get; private set; }

    public static WithdrawalPeriod Create(int milkDays, int meatDays)
    {
        if (milkDays < 0)
            throw new ArgumentException("Milk withdrawal days cannot be negative.", nameof(milkDays));

        if (meatDays < 0)
            throw new ArgumentException("Meat withdrawal days cannot be negative.", nameof(meatDays));

        return new WithdrawalPeriod(milkDays, meatDays);
    }

    public static WithdrawalPeriod None => new(0, 0);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return MilkDays;
        yield return MeatDays;
    }
}
