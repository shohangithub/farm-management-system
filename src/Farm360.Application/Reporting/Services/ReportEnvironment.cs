using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Reporting.Model;
using Farm360.Domain.Farms.Repositories;
using Farm360.Domain.Finance.Accounting;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using Farm360.Domain.Livestock.Repositories;
using Farm360.Domain.Organizations.Repositories;
using Farm360.Domain.Tenancy.Repositories;

namespace Farm360.Application.Reporting.Services;

/// <summary>A farm the current user may report on.</summary>
public sealed record ReportFarmOption(Guid Id, string Name);

/// <summary>Where and when a report is printed: the farm's zone and its financial-year start.</summary>
public sealed record ReportLocale(TimeZoneInfo TimeZone, int FiscalYearStartMonth)
{
    public static ReportLocale Default { get; } = new(ReportTimeZone.Default, FiscalYear.DefaultStartMonth);
}

/// <summary>
/// Everything the execution service needs to know about the caller's world that a definition
/// should not have to look up for itself: which farms they can see, the farm's time zone and
/// financial year, the user's real name, and what an id in the parameters is called.
/// </summary>
public interface IReportEnvironment
{
    Task<IReadOnlyList<ReportFarmOption>> GetAccessibleFarmsAsync(CancellationToken cancellationToken);

    Task<ReportLocale> GetLocaleAsync(Guid? farmId, CancellationToken cancellationToken);

    /// <summary>"Rafiq Ahmed (Manager)" — never a GUID.</summary>
    Task<string> GetUserDisplayNameAsync(CancellationToken cancellationToken);

    /// <summary>Human name for an entity id (farm name, batch name, animal tag…), or null if not found.</summary>
    Task<string?> DescribeAsync(ReportParameterType type, Guid id, CancellationToken cancellationToken);
}

internal sealed class ReportEnvironment : IReportEnvironment
{
    private readonly ITenantService _tenant;
    private readonly ICurrentUserService _currentUser;
    private readonly IUserProfileService _profiles;
    private readonly IFarmRepository _farms;
    private readonly IBranchRepository _branches;
    private readonly IOrganizationRepository _organizations;
    private readonly ITenantRepository _tenants;
    private readonly IFarmAccountingSettingsRepository _accountingSettings;
    private readonly IAnimalRepository _animals;
    private readonly IAnimalBatchRepository _batches;
    private readonly IShedRepository _sheds;
    private readonly IBreedRepository _breeds;
    private readonly IInventoryItemRepository _items;

    private IReadOnlyList<ReportFarmOption>? _farmCache;

    public ReportEnvironment(
        ITenantService tenant,
        ICurrentUserService currentUser,
        IUserProfileService profiles,
        IFarmRepository farms,
        IBranchRepository branches,
        IOrganizationRepository organizations,
        ITenantRepository tenants,
        IFarmAccountingSettingsRepository accountingSettings,
        IAnimalRepository animals,
        IAnimalBatchRepository batches,
        IShedRepository sheds,
        IBreedRepository breeds,
        IInventoryItemRepository items)
    {
        _tenant = tenant;
        _currentUser = currentUser;
        _profiles = profiles;
        _farms = farms;
        _branches = branches;
        _organizations = organizations;
        _tenants = tenants;
        _accountingSettings = accountingSettings;
        _animals = animals;
        _batches = batches;
        _sheds = sheds;
        _breeds = breeds;
        _items = items;
    }

    public async Task<IReadOnlyList<ReportFarmOption>> GetAccessibleFarmsAsync(CancellationToken cancellationToken)
    {
        if (_farmCache is not null)
        {
            return _farmCache;
        }

        var farms = await _farms.GetAllByTenantAsync(_tenant.TenantId, cancellationToken).ConfigureAwait(false);
        var assigned = _currentUser.AssignedFarmIds;

        _farmCache = farms
            .Where(f => assigned is null || assigned.Contains(f.Id))
            .OrderBy(f => f.FarmName, StringComparer.OrdinalIgnoreCase)
            .Select(f => new ReportFarmOption(f.Id, f.FarmName))
            .ToList();

        return _farmCache;
    }

    public async Task<ReportLocale> GetLocaleAsync(Guid? farmId, CancellationToken cancellationToken)
    {
        string? organizationZone = null;
        var fiscalStart = FiscalYear.DefaultStartMonth;

        if (farmId is { } id)
        {
            var farm = await _farms.GetByIdAsync(_tenant.TenantId, id, cancellationToken).ConfigureAwait(false);
            if (farm is not null)
            {
                var branch = await _branches.GetByIdAsync(_tenant.TenantId, farm.BranchId, cancellationToken).ConfigureAwait(false);
                if (branch is not null)
                {
                    var organization = await _organizations.GetByIdAsync(branch.OrganizationId, cancellationToken).ConfigureAwait(false);
                    organizationZone = organization?.TimeZoneId;
                }
            }

            var settings = await _accountingSettings.GetByFarmIdAsync(id, cancellationToken).ConfigureAwait(false);
            if (settings is not null)
            {
                fiscalStart = settings.FiscalYearStartMonth;
            }
        }

        string? tenantZone = null;
        if (organizationZone is null)
        {
            var tenant = await _tenants.GetByIdAsync(_tenant.TenantId, cancellationToken).ConfigureAwait(false);
            tenantZone = tenant?.TimeZone;
        }

        return new ReportLocale(ReportTimeZone.Resolve(organizationZone, tenantZone), fiscalStart);
    }

    public async Task<string> GetUserDisplayNameAsync(CancellationToken cancellationToken)
    {
        if (_currentUser.IsSystemUser)
        {
            return "System";
        }

        if (_currentUser.UserId is not { } userId)
        {
            return "Unknown user";
        }

        string? name = null;
        try
        {
            var profile = await _profiles.GetAsync(userId, cancellationToken).ConfigureAwait(false);
            name = string.IsNullOrWhiteSpace(profile.FullName) ? profile.Email : profile.FullName;
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
        {
            // A missing profile must not stop a report; fall through to the generic label.
        }

        name = string.IsNullOrWhiteSpace(name) ? "Unknown user" : name.Trim();
        return _currentUser.Role is { Length: > 0 } role ? $"{name} ({role})" : name;
    }

    public async Task<string?> DescribeAsync(ReportParameterType type, Guid id, CancellationToken cancellationToken)
    {
        switch (type)
        {
            case ReportParameterType.Farm:
            {
                var farms = await GetAccessibleFarmsAsync(cancellationToken).ConfigureAwait(false);
                var match = farms.FirstOrDefault(f => f.Id == id);
                if (match is not null)
                {
                    return match.Name;
                }

                var farm = await _farms.GetByIdAsync(_tenant.TenantId, id, cancellationToken).ConfigureAwait(false);
                return farm?.FarmName;
            }

            case ReportParameterType.Batch:
                return (await _batches.GetByIdAsync(id, cancellationToken).ConfigureAwait(false))?.Name;

            case ReportParameterType.Animal:
            {
                var animal = await _animals.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
                return animal is null ? null : $"Tag {animal.Tag.TagId}";
            }

            case ReportParameterType.Shed:
                return (await _sheds.GetByIdAsync(_tenant.TenantId, id, cancellationToken).ConfigureAwait(false))?.ShedNumber;

            case ReportParameterType.Breed:
                return (await _breeds.GetByIdAsync(id, cancellationToken).ConfigureAwait(false))?.Name;

            case ReportParameterType.InventoryItem:
                return (await _items.GetByIdAsync(id, cancellationToken).ConfigureAwait(false))?.Name;

            default:
                return null;
        }
    }
}
