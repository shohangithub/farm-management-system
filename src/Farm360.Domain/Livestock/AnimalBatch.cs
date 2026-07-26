using Farm360.Domain.Common;
using Farm360.Domain.Livestock.Enums;

namespace Farm360.Domain.Livestock;

/// <summary>
/// AnimalBatch — aggregate root for managing groups of animals.
/// Constitution §2.4 Aggregate Boundaries: Root: AnimalBatch
/// F360-MTA-2026-001: ITenantEntity
/// </summary>
public sealed class AnimalBatch : AuditableEntity, IAggregateRoot
{
    private AnimalBatch() { }

    private AnimalBatch(Guid id, Guid tenantId, Guid farmId, string name, string? notes)
        : base(id, tenantId)
    {
        FarmId = farmId;
        Name = name;
        Status = BatchStatus.Active;
        Notes = notes;
    }

    public Guid FarmId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public BatchStatus Status { get; private set; }
    public string? Notes { get; private set; }

    // Navigation property for EF Core, exposing animals assigned to this batch.
    private readonly List<Animal> _animals = [];
    public IReadOnlyCollection<Animal> Animals => _animals.AsReadOnly();

    public static AnimalBatch Create(Guid tenantId, Guid farmId, string name, string? notes)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Batch name is required.", nameof(name));

        return new AnimalBatch(Guid.NewGuid(), tenantId, farmId, name, notes);
    }

    public void Archive()
    {
        if (Status == BatchStatus.Archived)
            throw new InvalidOperationException("Batch is already archived.");
            
        Status = BatchStatus.Archived;
    }

    public void UpdateDetails(string name, string? notes)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Batch name is required.", nameof(name));

        Name = name;
        Notes = notes;
    }
}
