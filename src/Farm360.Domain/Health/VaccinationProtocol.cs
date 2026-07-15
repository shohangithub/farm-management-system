using Farm360.Domain.Common;
using Farm360.Domain.Livestock.Enums;

namespace Farm360.Domain.Health;

/// <summary>
/// Child entity of VaccinationProtocol template representing a single step (e.g. Dose 1 at day 0, Booster at day 21).
/// </summary>
public sealed class VaccinationProtocolStep : BaseEntity
{
    private VaccinationProtocolStep() { } // EF Core

    internal VaccinationProtocolStep(
        Guid id,
        Guid protocolId,
        int stepOrder,
        string stepName,
        int targetAgeDays,
        string vaccineName,
        string dosageInstruction)
        : base(id)
    {
        ProtocolId = protocolId;
        StepOrder = stepOrder;
        StepName = stepName;
        TargetAgeDays = targetAgeDays;
        VaccineName = vaccineName;
        DosageInstruction = dosageInstruction;
    }

    public Guid ProtocolId { get; private set; }
    public int StepOrder { get; private set; }
    public string StepName { get; private set; } = string.Empty;
    public int TargetAgeDays { get; private set; }
    public string VaccineName { get; private set; } = string.Empty;
    public string DosageInstruction { get; private set; } = string.Empty;
}

/// <summary>
/// Vaccination Protocol Aggregate Root — repeatable standard templates for species/breeds (e.g. FMD Standard Protocol).
/// </summary>
public sealed class VaccinationProtocol : AuditableEntity, IAggregateRoot
{
    private readonly List<VaccinationProtocolStep> _steps = [];

    private VaccinationProtocol() { } // EF Core

    private VaccinationProtocol(
        Guid id,
        Guid tenantId,
        string title,
        AnimalSpecies targetSpecies,
        string? description)
        : base(id, tenantId)
    {
        Title = title;
        TargetSpecies = targetSpecies;
        Description = description;
        IsActive = true;
    }

    public string Title { get; private set; } = string.Empty;
    public AnimalSpecies TargetSpecies { get; private set; }
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }

    public IReadOnlyCollection<VaccinationProtocolStep> Steps => _steps.AsReadOnly();

    public static VaccinationProtocol Create(
        Guid tenantId,
        string title,
        AnimalSpecies targetSpecies,
        string? description)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Protocol title is required.", nameof(title));

        return new VaccinationProtocol(Guid.NewGuid(), tenantId, title.Trim(), targetSpecies, description?.Trim());
    }

    public VaccinationProtocolStep AddStep(
        string stepName,
        int targetAgeDays,
        string vaccineName,
        string dosageInstruction)
    {
        if (string.IsNullOrWhiteSpace(vaccineName))
            throw new ArgumentException("Vaccine name is required.", nameof(vaccineName));

        var step = new VaccinationProtocolStep(
            Guid.NewGuid(),
            Id,
            _steps.Count + 1,
            stepName,
            targetAgeDays,
            vaccineName.Trim(),
            dosageInstruction);

        _steps.Add(step);
        return step;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
