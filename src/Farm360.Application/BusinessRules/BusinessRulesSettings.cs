using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.BusinessRules;
using FluentValidation;
using MediatR;

namespace Farm360.Application.BusinessRules;

// ═══ DTOs ══════════════════════════════════════════════════════════════════

/// <summary>A complete, resolved set of the tenant-editable business rules.</summary>
public sealed record BusinessRulesValuesDto(
    FeedAllocationMethod FeedAllocation,
    double MetabolicWeightExponent,
    GroupPlanQuantityBasis GroupPlanQuantityBasis,
    decimal FallbackAnimalWeightKg,
    OverheadAllocationMethod OverheadAllocation,
    OverheadCostPeriod OverheadCostPeriod,
    AnimalValuationPolicy Valuation,
    decimal DefaultDressingPercentage,
    decimal DefaultMeatPricePerKgBdt,
    decimal DefaultLiveWeightPricePerKgBdt,
    ReportLanguagePreference DefaultReportLanguage,
    bool BengaliNumeralsByDefault,
    bool CarryLivestockGrowingCosts = true)
{
    public static BusinessRulesValuesDto From(FarmBusinessRules r) => new(
        r.FeedAllocation, r.MetabolicWeightExponent, r.GroupPlanQuantityBasis, r.FallbackAnimalWeightKg,
        r.OverheadAllocation, r.OverheadCostPeriod, r.Valuation, r.DefaultDressingPercentage,
        r.DefaultMeatPricePerKgBdt, r.DefaultLiveWeightPricePerKgBdt, r.DefaultReportLanguage, r.BengaliNumeralsByDefault,
        r.CarryLivestockGrowingCosts);
}

/// <summary>
/// The business rules as one tenant (or one of its farms) sees them.
/// </summary>
/// <param name="FarmId">Null for the tenant-wide settings.</param>
/// <param name="Effective">What calculations use now.</param>
/// <param name="Inherited">What applies with no override at this level: the platform defaults for the tenant, the tenant's rules for a farm. "Reset to defaults" returns to these.</param>
/// <param name="Overrides">The values stored at this level; null fields inherit.</param>
/// <param name="IsCustomized">True when at least one field is overridden at this level.</param>
public sealed record BusinessRulesSettingsDto(
    Guid? FarmId,
    BusinessRulesValuesDto Effective,
    BusinessRulesValuesDto Inherited,
    BusinessRulesOverrides Overrides,
    bool IsCustomized,
    DateTime? LastModifiedAtUtc);

// ═══ Query ═════════════════════════════════════════════════════════════════

public sealed record GetBusinessRulesSettingsQuery(Guid? FarmId = null) : IRequest<BusinessRulesSettingsDto>;

public sealed class GetBusinessRulesSettingsQueryHandler(
    ITenantBusinessSettingsRepository repository,
    IFarmBusinessRulesProvider rules,
    ITenantService tenantService) : IRequestHandler<GetBusinessRulesSettingsQuery, BusinessRulesSettingsDto>
{
    public async Task<BusinessRulesSettingsDto> Handle(GetBusinessRulesSettingsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = BusinessRulesSettingsMapping.RequireTenant(tenantService);
        var row = await repository.GetAsync(request.FarmId, cancellationToken).ConfigureAwait(false);
        return BusinessRulesSettingsMapping.ToDto(row, request.FarmId, tenantId, rules);
    }
}

// ═══ Command ═══════════════════════════════════════════════════════════════

/// <summary>
/// Saves the tenant's (or one farm's) business-rule overrides. A null field reverts to the inherited
/// default; sending every field null is "Reset to defaults".
/// </summary>
/// <remarks>
/// Affects future calculations only: feed and overhead allocations already written keep the method
/// and prices that produced them, so signed-off reports do not change.
/// </remarks>
public sealed record SaveBusinessRulesSettingsCommand(Guid? FarmId, BusinessRulesOverrides Values)
    : IRequest<BusinessRulesSettingsDto>;

public sealed class SaveBusinessRulesSettingsCommandHandler(
    ITenantBusinessSettingsRepository repository,
    IFarmBusinessRulesProvider rules,
    ITenantService tenantService,
    IUnitOfWork unitOfWork) : IRequestHandler<SaveBusinessRulesSettingsCommand, BusinessRulesSettingsDto>
{
    public async Task<BusinessRulesSettingsDto> Handle(SaveBusinessRulesSettingsCommand command, CancellationToken cancellationToken)
    {
        var tenantId = BusinessRulesSettingsMapping.RequireTenant(tenantService);

        var row = await repository.GetAsync(command.FarmId, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            row = TenantBusinessSettings.Create(tenantId, command.FarmId, command.Values);
            repository.Add(row);
        }
        else
        {
            row.Update(command.Values);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Saved: drop the cached rules so this and every following request sees the change.
        rules.Invalidate(tenantId);

        return BusinessRulesSettingsMapping.ToDto(row, command.FarmId, tenantId, rules);
    }
}

public sealed class SaveBusinessRulesSettingsCommandValidator : AbstractValidator<SaveBusinessRulesSettingsCommand>
{
    public const decimal MaxPricePerKgBdt = 100_000m;

    public SaveBusinessRulesSettingsCommandValidator()
    {
        RuleFor(c => c.FarmId).NotEqual(Guid.Empty).When(c => c.FarmId.HasValue);
        RuleFor(c => c.Values).NotNull();

        When(c => c.Values is not null, () =>
        {
            RuleFor(c => c.Values.FeedAllocation).IsInEnum().When(c => c.Values.FeedAllocation.HasValue)
                .WithName("Feed allocation method");
            RuleFor(c => c.Values.GroupPlanQuantityBasis).IsInEnum().When(c => c.Values.GroupPlanQuantityBasis.HasValue)
                .WithName("Group plan quantity basis");
            RuleFor(c => c.Values.OverheadAllocation).IsInEnum().When(c => c.Values.OverheadAllocation.HasValue)
                .WithName("Overhead allocation method");
            RuleFor(c => c.Values.OverheadCostPeriod).IsInEnum().When(c => c.Values.OverheadCostPeriod.HasValue)
                .WithName("Overhead cost period");
            RuleFor(c => c.Values.Valuation).IsInEnum().When(c => c.Values.Valuation.HasValue)
                .WithName("Valuation policy");
            RuleFor(c => c.Values.DefaultReportLanguage).IsInEnum().When(c => c.Values.DefaultReportLanguage.HasValue)
                .WithName("Report language");

            RuleFor(c => c.Values.MetabolicWeightExponent)
                .InclusiveBetween(0.5d, 1.0d).When(c => c.Values.MetabolicWeightExponent.HasValue)
                .WithName("Metabolic weight exponent")
                .WithMessage("Metabolic weight exponent must be between 0.5 and 1.0 (0.75 is the standard).");

            RuleFor(c => c.Values.FallbackAnimalWeightKg)
                .InclusiveBetween(1m, 2_000m).When(c => c.Values.FallbackAnimalWeightKg.HasValue)
                .WithName("Fallback animal weight")
                .WithMessage("Fallback animal weight must be between 1 and 2,000 kg.");

            RuleFor(c => c.Values.DefaultDressingPercentage)
                .InclusiveBetween(0.3m, 0.7m).When(c => c.Values.DefaultDressingPercentage.HasValue)
                .WithName("Dressing percentage")
                .WithMessage("Dressing percentage must be between 30% and 70% (0.30–0.70).");

            RuleFor(c => c.Values.DefaultMeatPricePerKgBdt)
                .GreaterThan(0m).LessThanOrEqualTo(MaxPricePerKgBdt).When(c => c.Values.DefaultMeatPricePerKgBdt.HasValue)
                .WithName("Meat price per kg");

            RuleFor(c => c.Values.DefaultLiveWeightPricePerKgBdt)
                .GreaterThan(0m).LessThanOrEqualTo(MaxPricePerKgBdt).When(c => c.Values.DefaultLiveWeightPricePerKgBdt.HasValue)
                .WithName("Live weight price per kg");
        });
    }
}

// ═══ Mapping ═══════════════════════════════════════════════════════════════

internal static class BusinessRulesSettingsMapping
{
    public static Guid RequireTenant(ITenantService tenantService) =>
        tenantService.TenantId != Guid.Empty
            ? tenantService.TenantId
            : throw new ForbiddenAccessException("Business rules are set per organisation; no organisation is selected.");

    public static BusinessRulesSettingsDto ToDto(
        TenantBusinessSettings? row, Guid? farmId, Guid tenantId, IFarmBusinessRulesProvider rules)
    {
        var inherited = farmId is null ? rules.Defaults : rules.For(tenantId);
        var overrides = row?.ToOverrides() ?? new BusinessRulesOverrides();
        var effective = FarmBusinessRulesResolution.Apply(inherited, overrides);

        return new BusinessRulesSettingsDto(
            farmId,
            BusinessRulesValuesDto.From(effective),
            BusinessRulesValuesDto.From(inherited),
            overrides,
            !overrides.IsEmpty,
            row is null ? null : row.ModifiedAtUtc ?? row.CreatedAtUtc);
    }
}
