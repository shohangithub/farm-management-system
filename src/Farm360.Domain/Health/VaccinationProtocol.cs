using Farm360.Domain.Common;
using Farm360.Domain.Health.Enums;
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
        string dosageInstruction,
        Guid? inventoryItemId = null,
        decimal? dosageQuantity = null,
        ProtocolStepOptions? options = null)
        : base(id)
    {
        options ??= ProtocolStepOptions.Default;
        ProtocolId = protocolId;
        StepOrder = stepOrder;
        StepName = stepName;
        TargetAgeDays = targetAgeDays;
        VaccineName = vaccineName;
        DosageInstruction = dosageInstruction;
        Kind = options.Kind;
        RepeatEveryDays = options.RepeatEveryDays;
        ActionLink = string.IsNullOrWhiteSpace(options.ActionLink) ? null : options.ActionLink.Trim();

        // A care task (potash wash, health check) uses no medicine: nothing to take from stock.
        if (Kind == HealthTaskKind.CareTask)
            return;

        InventoryItemId = inventoryItemId;
        DosageQuantity = dosageQuantity;
        DoseUnitId = inventoryItemId is null ? null : options.DoseUnitId;
        if (options.DoseRule is { } rule)
        {
            DoseAmount = rule.Amount;
            DosePerKg = rule.PerKg;
            DoseRounding = rule.Rounding;
            DoseMin = rule.Min;
            DoseMax = rule.Max;
            DoseUnit = rule.Unit;
        }
    }

    public Guid ProtocolId { get; private set; }
    public int StepOrder { get; private set; }
    public string StepName { get; private set; } = string.Empty;
    public int TargetAgeDays { get; private set; }
    public string VaccineName { get; private set; } = string.Empty;
    public string DosageInstruction { get; private set; } = string.Empty;
    public Guid? InventoryItemId { get; private set; }
    public decimal? DosageQuantity { get; private set; }

    /// <summary>Medicine/vaccine (default) or a care task with no medicine.</summary>
    public HealthTaskKind Kind { get; private set; } = HealthTaskKind.Medicine;

    /// <summary>When set, giving (or skipping) the dose schedules the next one this many days later.</summary>
    public int? RepeatEveryDays { get; private set; }

    // Weight-based dose ("1 tablet per 150 kg"); all null when the dose is fixed.
    public decimal? DoseAmount { get; private set; }
    public decimal? DosePerKg { get; private set; }
    public DoseRounding DoseRounding { get; private set; }
    public decimal? DoseMin { get; private set; }
    public decimal? DoseMax { get; private set; }
    public string? DoseUnit { get; private set; }

    /// <summary>The stock unit (a unit conversion of the linked item's base unit) the dose is counted in; null = the item's base unit.</summary>
    public Guid? DoseUnitId { get; private set; }

    /// <summary>An in-app shortcut for the task, e.g. "sell-or-keep" opens the animal's Sell or Keep card.</summary>
    public string? ActionLink { get; private set; }

    public WeightDoseRule? DoseRule => WeightDoseRule.From(DoseAmount, DosePerKg, DoseRounding, DoseMin, DoseMax, DoseUnit);
}

/// <summary>Optional settings of a protocol step beyond name, day and medicine.</summary>
public sealed record ProtocolStepOptions(
    HealthTaskKind Kind = HealthTaskKind.Medicine,
    int? RepeatEveryDays = null,
    WeightDoseRule? DoseRule = null,
    Guid? DoseUnitId = null,
    string? ActionLink = null)
{
    public static readonly ProtocolStepOptions Default = new();
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
    public bool IsDeworming { get; private set; }

    /// <summary>Started automatically (from the acquisition date) for every newly registered purchased animal.</summary>
    public bool AutoStartForPurchased { get; private set; }

    /// <summary>Days the programme spans, counting the start day (the last step's day + 1).</summary>
    public int SpanDays => _steps.Count == 0 ? 0 : _steps.Max(s => s.TargetAgeDays) + 1;

    public IReadOnlyCollection<VaccinationProtocolStep> Steps => _steps.AsReadOnly();

    public static VaccinationProtocol Create(
        Guid tenantId,
        string title,
        AnimalSpecies targetSpecies,
        string? description,
        bool isDeworming = false)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Protocol title is required.", nameof(title));

        var protocol = new VaccinationProtocol(Guid.NewGuid(), tenantId, title.Trim(), targetSpecies, description?.Trim());
        protocol.IsDeworming = isDeworming;
        return protocol;
    }

    public VaccinationProtocolStep AddStep(
        string stepName,
        int targetAgeDays,
        string vaccineName,
        string dosageInstruction,
        Guid? inventoryItemId = null,
        decimal? dosageQuantity = null,
        ProtocolStepOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(vaccineName))
            throw new ArgumentException("Vaccine name is required.", nameof(vaccineName));
        if (options?.RepeatEveryDays is < 1)
            throw new Farm360.Domain.Exceptions.DomainException("A step can repeat every 1 day or more.");

        var step = new VaccinationProtocolStep(
            Guid.NewGuid(),
            Id,
            _steps.Count + 1,
            stepName,
            targetAgeDays,
            vaccineName.Trim(),
            dosageInstruction,
            inventoryItemId,
            dosageQuantity,
            options);

        _steps.Add(step);
        return step;
    }

    public void Update(string title, AnimalSpecies targetSpecies, string? description)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Protocol title is required.", nameof(title));

        Title = title.Trim();
        TargetSpecies = targetSpecies;
        Description = description?.Trim();
    }

    public void ClearSteps()
    {
        _steps.Clear();
    }

    public void SetAutoStartForPurchased(bool autoStart) => AutoStartForPurchased = autoStart;

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
