using Farm360.Domain.Common;

namespace Farm360.Domain.Livestock.ValueObjects;

/// <summary>
/// Weight measurement for an animal.
/// Value Object: equality by WeightKg value.
/// Constitution §3.1: Immutable, self-validating via factory method.
/// Range: 0.1 kg (newborn kid) to 2000 kg (large bull) — enforced at construction.
/// </summary>
public sealed class Weight : BaseValueObject
{
    private Weight() { }  // EF Core

    private Weight(decimal weightKg)
    {
        WeightKg = weightKg;
    }

    /// <summary>Weight in kilograms (2 decimal precision).</summary>
    public decimal WeightKg { get; private set; }

    /// <summary>
    /// Factory method — only valid weight values can be constructed.
    /// </summary>
    public static Weight Create(decimal weightKg)
    {
        if (weightKg <= 0)
            throw new ArgumentException("Weight must be greater than zero.", nameof(weightKg));

        if (weightKg > 2000)
            throw new ArgumentException("Weight cannot exceed 2000 kg.", nameof(weightKg));

        return new Weight(Math.Round(weightKg, 2));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return WeightKg;
    }

    public override string ToString() => $"{WeightKg:F2} kg";
}
