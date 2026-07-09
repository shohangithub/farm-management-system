namespace Farm360.Domain.Common;

/// <summary>
/// Base class for Value Objects (DDD).
/// Value objects are immutable, equality by value not reference.
/// Examples: Money, AnimalTag, PhoneNumber, Weight.
/// </summary>
public abstract class BaseValueObject : IEquatable<BaseValueObject>
{
    /// <summary>Returns all component values used for equality comparison.</summary>
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public bool Equals(BaseValueObject? other)
    {
        if (other is null || other.GetType() != GetType())
        {
            return false;
        }

        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    public override bool Equals(object? obj) =>
        obj is BaseValueObject valueObject && Equals(valueObject);

    public override int GetHashCode() =>
        GetEqualityComponents()
            .Select(x => x?.GetHashCode() ?? 0)
            .Aggregate((x, y) => x ^ y);

    public static bool operator ==(BaseValueObject? left, BaseValueObject? right) =>
        left?.Equals(right) ?? right is null;

    public static bool operator !=(BaseValueObject? left, BaseValueObject? right) =>
        !(left == right);
}
