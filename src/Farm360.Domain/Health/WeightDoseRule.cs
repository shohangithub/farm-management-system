using Farm360.Domain.Exceptions;
using Farm360.Domain.Health.Enums;

namespace Farm360.Domain.Health;

/// <summary>
/// A dose set by body weight: <see cref="Amount"/> units for every <see cref="PerKg"/> kg
/// (e.g. 1 tablet per 150 kg), optionally rounded up to a half or whole unit and kept within
/// <see cref="Min"/>..<see cref="Max"/>. <see cref="Unit"/> is only a label ("tablet", "ml").
/// </summary>
public sealed record WeightDoseRule
{
    public WeightDoseRule(decimal amount, decimal perKg, DoseRounding rounding = DoseRounding.None,
        decimal? min = null, decimal? max = null, string? unit = null)
    {
        if (amount <= 0)
            throw new DomainException("The dose per body weight must be greater than zero.");
        if (perKg <= 0)
            throw new DomainException("The body weight for the dose must be greater than zero.");
        if (min is < 0 || max is < 0)
            throw new DomainException("The minimum and maximum dose cannot be negative.");
        if (min is { } lo && max is { } hi && lo > hi)
            throw new DomainException("The minimum dose cannot be more than the maximum dose.");

        Amount = amount;
        PerKg = perKg;
        Rounding = rounding;
        Min = min;
        Max = max;
        Unit = string.IsNullOrWhiteSpace(unit) ? null : unit.Trim();
    }

    public decimal Amount { get; }
    public decimal PerKg { get; }
    public DoseRounding Rounding { get; }
    public decimal? Min { get; }
    public decimal? Max { get; }
    public string? Unit { get; }

    /// <summary>A rule from stored columns; null when no per-weight dose is set.</summary>
    public static WeightDoseRule? From(decimal? amount, decimal? perKg, DoseRounding rounding,
        decimal? min, decimal? max, string? unit) =>
        amount is > 0 && perKg is > 0
            ? new WeightDoseRule(amount.Value, perKg.Value, rounding, min, max, unit)
            : null;

    /// <summary>The dose for an animal of <paramref name="weightKg"/> kg.</summary>
    public decimal DoseFor(decimal weightKg)
    {
        if (weightKg <= 0)
            throw new DomainException("A weight-based dose needs the animal's weight.");

        var raw = weightKg / PerKg * Amount;
        var dose = Rounding switch
        {
            DoseRounding.Whole => Math.Ceiling(raw),
            DoseRounding.Half => Math.Ceiling(raw * 2m) / 2m,
            _ => Math.Round(raw, 2, MidpointRounding.AwayFromZero),
        };

        if (Min is { } min && dose < min) dose = min;
        if (Max is { } max && dose > max) dose = max;
        return dose;
    }
}
