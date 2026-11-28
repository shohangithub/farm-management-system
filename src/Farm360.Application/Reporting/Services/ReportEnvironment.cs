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
using Farm360.Domain.Organizations;
using Farm360.Domain.Organizations.Repositories;
using Farm360.Domain.Organizations.ValueObjects;
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
/// Who is printing: the organisation's name, contact line and logo, as they appear at the top of
/// every report and customer document.
/// </summary>
/// <param name="OrganizationName">The organisation that owns the farm (null when none is set up).</param>
/// <param name="ContactLine">Address · phone · e-mail, whichever exist; null when none do.</param>
/// <param name="LogoUrl">Relative URL of the stored logo, for the browser.</param>
/// <param name="Logo">The logo bytes for PDFs; null when there is no logo or it could not be read.</param>
public sealed record ReportLetterhead(
    string? OrganizationName,
    string? ContactLine,
    string? LogoUrl,
    byte[]? Logo)
{
    public static ReportLetterhead None { get; } = new(null, null, null, null);
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

    /// <summary>
    /// The organisation letterhead for a farm (or, without a farm, the tenant's organisation):
    /// name, contact line and logo. Never throws for a missing logo; cached for the request.
    /// </summary>
    Task<ReportLetterhead> GetLetterheadAsync(Guid? farmId, CancellationToken cancellationToken) =>
        Task.FromResult(ReportLetterhead.None);
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
    private readonly IFileStorageService? _files;
    private readonly IMoneyAccountRepository? _moneyAccounts;
    private readonly IInvestorRepository? _investors;

    private IReadOnlyList<ReportFarmOption>? _farmCache;
    private readonly Dictionary<Guid, ReportLetterhead> _letterheadCache = [];
    private readonly Dictionary<string, byte[]?> _logoCache = new(StringComparer.OrdinalIgnoreCase);

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
        IInventoryItemRepository items,
        IFileStorageService? files = null,
        IMoneyAccountRepository? moneyAccounts = null,
        IInvestorRepository? investors = null)
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
        _files = files;
        _moneyAccounts = moneyAccounts;
        _investors = investors;
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

            case ReportParameterType.MoneyAccount:
                return _moneyAccounts is null ? null : (await _moneyAccounts.GetByIdAsync(id, cancellationToken).ConfigureAwait(false))?.Name;

            case ReportParameterType.Partner:
                return _investors is null ? null : (await _investors.GetByIdAsync(id, cancellationToken).ConfigureAwait(false))?.Name;

            default:
                return null;
        }
    }

    private const long LogoMaxBytes = 2 * 1024 * 1024;

    public async Task<ReportLetterhead> GetLetterheadAsync(Guid? farmId, CancellationToken cancellationToken)
    {
        var cacheKey = farmId ?? Guid.Empty;
        if (_letterheadCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        Organization? organization = null;
        Branch? branch = null;

        if (farmId is { } id)
        {
            var farm = await _farms.GetByIdAsync(_tenant.TenantId, id, cancellationToken).ConfigureAwait(false);
            if (farm is not null)
            {
                branch = await _branches.GetByIdAsync(_tenant.TenantId, farm.BranchId, cancellationToken).ConfigureAwait(false);
                if (branch is not null)
                {
                    organization = await _organizations.GetByIdAsync(branch.OrganizationId, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        if (organization is null)
        {
            // No farm, or a farm without a branch: the tenant's own (oldest) organisation, if any.
            var all = await _organizations.GetAllByTenantAsync(_tenant.TenantId, cancellationToken).ConfigureAwait(false);
            organization = all
                .OrderBy(o => o.CreatedAtUtc)
                .ThenBy(o => o.Name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        ReportLetterhead letterhead;
        if (organization is null && branch is null)
        {
            letterhead = ReportLetterhead.None;
        }
        else
        {
            var logoUrl = string.IsNullOrWhiteSpace(organization?.LogoUrl) ? null : organization.LogoUrl;
            letterhead = new ReportLetterhead(
                string.IsNullOrWhiteSpace(organization?.Name) ? null : organization.Name.Trim(),
                BuildContactLine(organization, branch),
                logoUrl,
                await LoadLogoAsync(logoUrl, cancellationToken).ConfigureAwait(false));
        }

        _letterheadCache[cacheKey] = letterhead;
        return letterhead;
    }

    /// <summary>"Road 4, Savar, Dhaka · 01711-000000 · info@farm.com" — whichever parts exist.</summary>
    internal static string? BuildContactLine(Organization? organization, Branch? branch)
    {
        var parts = new List<string>(3);

        var address = FormatAddress(branch?.Address) ?? FormatAddress(organization?.Address);
        if (address is not null)
        {
            parts.Add(address);
        }

        var phone = FirstNonBlank(branch?.ContactPhone, organization?.ContactPhone);
        if (phone is not null)
        {
            parts.Add(phone);
        }

        var email = FirstNonBlank(organization?.ContactEmail, branch?.ContactEmail);
        if (email is not null)
        {
            parts.Add(email);
        }

        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    private static string? FormatAddress(Address? address)
    {
        if (address is null)
        {
            return null;
        }

        string?[] candidates = [address.Street, address.City, address.State, address.ZipCode, address.Country];
        var parts = new List<string>(candidates.Length);
        foreach (var part in candidates)
        {
            if (!string.IsNullOrWhiteSpace(part))
            {
                parts.Add(part.Trim());
            }
        }

        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    private static string? FirstNonBlank(string? first, string? second)
    {
        if (!string.IsNullOrWhiteSpace(first))
        {
            return first.Trim();
        }

        return string.IsNullOrWhiteSpace(second) ? null : second.Trim();
    }

    private async Task<byte[]?> LoadLogoAsync(string? logoUrl, CancellationToken cancellationToken)
    {
        if (_files is null || string.IsNullOrWhiteSpace(logoUrl))
        {
            return null;
        }

        if (_logoCache.TryGetValue(logoUrl, out var cached))
        {
            return cached;
        }

        // Only a file the app itself stored under /uploads/organizations — never a remote URL.
        byte[]? bytes = null;
        if (logoUrl.TrimStart('/').StartsWith("uploads/organizations/", StringComparison.OrdinalIgnoreCase))
        {
            bytes = await _files.ReadFileAsync(logoUrl, LogoMaxBytes, cancellationToken).ConfigureAwait(false);
        }

        _logoCache[logoUrl] = bytes;
        return bytes;
    }
}
