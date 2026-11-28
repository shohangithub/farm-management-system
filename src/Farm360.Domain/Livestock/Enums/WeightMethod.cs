namespace Farm360.Domain.Livestock.Enums;

/// <summary>How a weight was obtained.</summary>
public enum WeightMethod
{
    /// <summary>Weighed on a scale; the kg was entered directly. Every weight recorded before tape support is a scale weight.</summary>
    Scale = 0,

    /// <summary>Estimated from a tape measure (body length and heart girth) with <see cref="TapeWeight"/>.</summary>
    Tape = 1
}
