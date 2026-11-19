using Farm360.Domain.BusinessRules;

namespace Farm360.Application.Common.Interfaces;

/// <summary>
/// Supplies the farm's configurable business assumptions to application code.
/// </summary>
/// <remarks>
/// <para>
/// An interface rather than an injected options object so the Application layer stays free of
/// <c>Microsoft.Extensions.Options</c> and so tests can state their assumptions in one line.
/// </para>
/// <para>
/// Rules are per tenant: each customer may override the platform defaults (the
/// <c>Farm360:BusinessRules</c> configuration section) from Settings → Farm rules, and a farm may
/// override its tenant. Resolution is field by field: farm → tenant → configuration.
/// </para>
/// <para>
/// <see cref="Current"/> resolves, in order: an ambient scope opened with
/// <see cref="BeginTenantScope"/>; otherwise the request tenant from <see cref="ITenantService"/>
/// (which background jobs set with <c>SetTenant</c> before working on a tenant); otherwise the
/// configuration defaults. Code that knows the tenant it is working on — especially jobs that loop
/// across tenants — should prefer <see cref="For"/>.
/// </para>
/// <para>
/// The extra members have default implementations so simple test doubles that only supply
/// <see cref="Current"/> keep compiling and keep their single-tenant behaviour.
/// </para>
/// </remarks>
public interface IFarmBusinessRulesProvider
{
    /// <summary>The rules for the ambient tenant (job scope, then request tenant), else the configured defaults.</summary>
    FarmBusinessRules Current { get; }

    /// <summary>The rules for a specific tenant, and optionally one of its farms.</summary>
    FarmBusinessRules For(Guid tenantId, Guid? farmId = null) => Current;

    /// <summary>The platform defaults from configuration, before any tenant or farm override.</summary>
    FarmBusinessRules Defaults => Current;

    /// <summary>
    /// Makes <see cref="Current"/> resolve for <paramref name="tenantId"/> (and optionally a farm)
    /// until the returned scope is disposed. Flows across awaits (AsyncLocal). For background code
    /// that has no request tenant: <c>using (rules.BeginTenantScope(plan.TenantId, plan.FarmId)) { ... }</c>.
    /// Scopes nest; disposing restores the previous one.
    /// </summary>
    IDisposable BeginTenantScope(Guid tenantId, Guid? farmId = null) => NoopScope.Instance;

    /// <summary>Drops the cached rules of <paramref name="tenantId"/> so the next read sees a saved change.</summary>
    void Invalidate(Guid tenantId) { }

    private sealed class NoopScope : IDisposable
    {
        public static readonly NoopScope Instance = new();
        public void Dispose() { }
    }
}
