using Farm360.Domain.Common;

namespace Farm360.Domain.Livestock;

/// <summary>
/// Child entity representing a Body Condition Score (BCS) assessment.
/// </summary>
public sealed class BodyConditionScore : BaseEntity
{
    private BodyConditionScore() { }

    internal BodyConditionScore(
        Guid id,
        Guid animalId,
        decimal score,
        DateOnly recordedDate,
        Guid evaluatorId,
        string? notes) : base(id)
    {
        AnimalId = animalId;
        Score = score;
        RecordedDate = recordedDate;
        EvaluatorId = evaluatorId;
        Notes = notes;
    }

    public Guid AnimalId { get; private set; }
    
    /// <summary>
    /// BCS Score, typically on a scale of 1.0 to 5.0 in increments of 0.25.
    /// </summary>
    public decimal Score { get; private set; }
    
    public DateOnly RecordedDate { get; private set; }
    public Guid EvaluatorId { get; private set; }
    public string? Notes { get; private set; }
}
