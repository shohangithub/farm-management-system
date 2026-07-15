using Farm360.Domain.Common;
using Farm360.Domain.Livestock.Enums;

namespace Farm360.Domain.Livestock.ValueObjects;

/// <summary>
/// Unique animal identification tag.
/// Value Object: equality is structural (TagId + TagType pair).
/// Constitution §3.1 Domain Layer: sealed class extending BaseValueObject, immutable, self-validating.
/// Business rule: TagId is unique per tenant (enforced by DB UQ index and async FluentValidation).
/// MaxLength = 50 — supports RFID (24 hex chars) and manual label strings.
/// </summary>
public sealed class AnimalTag : BaseValueObject
{
    private AnimalTag() { }  // Required by EF Core owned-type materialisation

    private AnimalTag(string tagId, TagType tagType)
    {
        TagId = tagId;
        TagType = tagType;
    }

    public string TagId { get; private set; } = string.Empty;
    public TagType TagType { get; private set; }

    /// <summary>
    /// Factory method — the only correct way to construct an AnimalTag.
    /// Constitution §3.1: No invalid state can be constructed.
    /// </summary>
    public static AnimalTag Create(string tagId, TagType tagType)
    {
        if (string.IsNullOrWhiteSpace(tagId))
            throw new ArgumentException("Tag ID cannot be empty.", nameof(tagId));

        if (tagId.Length > 50)
            throw new ArgumentException("Tag ID cannot exceed 50 characters.", nameof(tagId));

        return new AnimalTag(tagId.Trim().ToUpperInvariant(), tagType);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return TagId;
        yield return TagType;
    }

    public override string ToString() => $"{TagType}:{TagId}";
}
