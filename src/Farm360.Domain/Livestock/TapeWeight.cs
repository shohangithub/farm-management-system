using Farm360.Domain.Livestock.Exceptions;

namespace Farm360.Domain.Livestock;

/// <summary>
/// Live-weight estimate from a tape measure — the single home of the formula.
/// <para>
/// weight (kg) = body length (in) × heart girth (in)² ÷ 660, rounded to 0.1 kg. This is the
/// Schaeffer formula used across Bangladesh: the classic (L × G²) ÷ 300 in pounds, divided by 2.2.
/// Body length is measured from the point of the shoulder to the pin bone; heart girth around the
/// chest just behind the front legs. A tape estimate is usually within about ±5–10% of a scale.
/// </para>
/// </summary>
public static class TapeWeight
{
    /// <summary>The divisor that turns inches³ into kilograms.</summary>
    public const decimal Divisor = 660m;

    /// <summary>Smallest plausible body length or heart girth, in inches.</summary>
    public const decimal MinInch = 20m;

    /// <summary>Largest plausible body length or heart girth, in inches.</summary>
    public const decimal MaxInch = 120m;

    /// <summary>Centimetres per inch, for callers that measured in cm.</summary>
    public const decimal CmPerInch = 2.54m;

    public const string LengthOutOfRangeMessage = "Body length must be between 20 and 120 inches.";
    public const string GirthOutOfRangeMessage = "Heart girth must be between 20 and 120 inches.";
    public const string EstimateTooHighMessage = "These measurements give more than 2000 kg. Check the length and heart girth.";

    /// <summary>Largest weight an animal may be recorded at (same cap as a scale weight).</summary>
    public const decimal MaxKg = 2000m;

    /// <summary>True when a length or girth (inches) is inside the plausible range.</summary>
    public static bool IsPlausible(decimal inches) => inches is >= MinInch and <= MaxInch;

    /// <summary>
    /// The estimate without range checks or exceptions — for validators that report their own messages.
    /// Same arithmetic and rounding as <see cref="EstimateKg"/>.
    /// </summary>
    public static decimal Compute(decimal lengthInch, decimal girthInch) =>
        Math.Round(lengthInch * girthInch * girthInch / Divisor, 1, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Estimated live weight in kg, rounded to one decimal (midpoint away from zero).
    /// Throws <see cref="InvalidTapeMeasurementException"/> when either measurement is out of range
    /// or the estimate is above the 2000 kg weight cap.
    /// </summary>
    public static decimal EstimateKg(decimal lengthInch, decimal girthInch)
    {
        if (!IsPlausible(lengthInch))
        {
            throw new InvalidTapeMeasurementException(LengthOutOfRangeMessage);
        }

        if (!IsPlausible(girthInch))
        {
            throw new InvalidTapeMeasurementException(GirthOutOfRangeMessage);
        }

        var kg = Compute(lengthInch, girthInch);
        if (kg > MaxKg)
        {
            throw new InvalidTapeMeasurementException(EstimateTooHighMessage);
        }

        return kg;
    }
}
