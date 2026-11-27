using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.BusinessRules;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.BusinessRules;
using Farm360.Infrastructure.BusinessRules;
using FluentAssertions;
using FluentValidation.TestHelper;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Farm360.Application.UnitTests.BusinessRules;

/// <summary>Per-tenant business rules: resolution order, caching, job scopes, the save path and validation.</summary>
public sealed class TenantBusinessRulesTests : IDisposable
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    private static readonly Guid FarmA1 = Guid.NewGuid();

    private readonly FakeSettingsRepository _repository = new();
    private readonly FarmBusinessRules _config = new() { DefaultMeatPricePerKgBdt = 680m, DefaultDressingPercentage = 0.50m };
    private readonly FarmBusinessRulesCache _cache;
    private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());
    private readonly ServiceProvider _services;

    public TenantBusinessRulesTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITenantBusinessSettingsRepository>(_repository);
        _services = services.BuildServiceProvider();
        var options = Substitute.For<IOptionsMonitor<FarmBusinessRules>>();
        options.CurrentValue.Returns(_config);
        _cache = new FarmBusinessRulesCache(_memoryCache, _services.GetRequiredService<IServiceScopeFactory>(), options);
    }

    public void Dispose()
    {
        _memoryCache.Dispose();
        _services.Dispose();
    }

    private FarmBusinessRulesProvider ProviderFor(Guid requestTenant)
    {
        var tenant = Substitute.For<ITenantService>();
        tenant.TenantId.Returns(requestTenant);
        return new FarmBusinessRulesProvider(_cache, tenant);
    }

    // ── Resolution order ────────────────────────────────────────────────────

    [Fact]
    public void No_row_falls_back_to_configuration()
    {
        ProviderFor(TenantA).Current.DefaultMeatPricePerKgBdt.Should().Be(680m);
    }

    [Fact]
    public void Tenant_row_overrides_config_field_by_field()
    {
        _repository.Rows.Add(TenantBusinessSettings.Create(TenantA, null,
            new BusinessRulesOverrides(DefaultMeatPricePerKgBdt: 750m, Valuation: AnimalValuationPolicy.CostBasis)));

        var rules = ProviderFor(TenantA).Current;

        rules.DefaultMeatPricePerKgBdt.Should().Be(750m);
        rules.Valuation.Should().Be(AnimalValuationPolicy.CostBasis);
        rules.DefaultDressingPercentage.Should().Be(0.50m, "not overridden, so inherited from configuration");
        _config.DefaultMeatPricePerKgBdt.Should().Be(680m, "resolution must never mutate the shared config instance");
    }

    [Fact]
    public void Farm_row_overrides_tenant_which_overrides_config()
    {
        _repository.Rows.Add(TenantBusinessSettings.Create(TenantA, null,
            new BusinessRulesOverrides(DefaultMeatPricePerKgBdt: 750m, DefaultDressingPercentage: 0.55m)));
        _repository.Rows.Add(TenantBusinessSettings.Create(TenantA, FarmA1,
            new BusinessRulesOverrides(DefaultMeatPricePerKgBdt: 800m)));

        var provider = ProviderFor(TenantA);
        var farm = provider.For(TenantA, FarmA1);

        farm.DefaultMeatPricePerKgBdt.Should().Be(800m);
        farm.DefaultDressingPercentage.Should().Be(0.55m);
        farm.FeedAllocation.Should().Be(FeedAllocationMethod.MetabolicWeight);

        provider.For(TenantA, Guid.NewGuid()).DefaultMeatPricePerKgBdt.Should().Be(750m, "another farm has no override");
        provider.Current.DefaultMeatPricePerKgBdt.Should().Be(750m, "Current is the tenant-wide rule");
    }

    [Fact]
    public void New_tenants_value_at_live_weight_while_existing_tenants_keep_the_inherited_method()
    {
        // TenantA was just created (sign-up / onboarding store this row); TenantB is an existing tenant with no row.
        _repository.Rows.Add(TenantBusinessSettings.CreateForNewTenant(TenantA));

        var created = ProviderFor(TenantA).Current;
        created.Valuation.Should().Be(AnimalValuationPolicy.LiveWeightMarket);
        created.DefaultLiveWeightPricePerKgBdt.Should().Be(_config.DefaultLiveWeightPricePerKgBdt, "the configured live price is kept");
        TenantBusinessSettings.NewTenantDefaults.Valuation.Should().Be(AnimalValuationPolicy.LiveWeightMarket);
        TenantBusinessSettings.NewTenantDefaults.DefaultLiveWeightPricePerKgBdt.Should().BeNull("only the method is set; the price is inherited");

        ProviderFor(TenantB).Current.Valuation.Should().Be(AnimalValuationPolicy.MeatYieldMarket,
            "an existing tenant without a stored override must not silently change method");
        new FarmBusinessRules().Valuation.Should().Be(AnimalValuationPolicy.MeatYieldMarket, "the platform default is unchanged");
    }

    [Fact]
    public void Tenants_are_isolated()
    {
        _repository.Rows.Add(TenantBusinessSettings.Create(TenantA, null, new BusinessRulesOverrides(DefaultMeatPricePerKgBdt: 750m)));

        ProviderFor(TenantB).Current.DefaultMeatPricePerKgBdt.Should().Be(680m);
        ProviderFor(TenantB).For(TenantA).DefaultMeatPricePerKgBdt.Should().Be(750m);
    }

    // ── Caching ─────────────────────────────────────────────────────────────

    [Fact]
    public void Rows_are_cached_per_tenant_and_reloaded_after_invalidate()
    {
        var row = TenantBusinessSettings.Create(TenantA, null, new BusinessRulesOverrides(DefaultMeatPricePerKgBdt: 750m));
        _repository.Rows.Add(row);
        var provider = ProviderFor(TenantA);

        _ = provider.Current;
        _ = provider.Current;
        _ = ProviderFor(TenantA).For(TenantA, FarmA1);
        _repository.Loads.Should().Be(1);

        row.Update(new BusinessRulesOverrides(DefaultMeatPricePerKgBdt: 900m));
        provider.Current.DefaultMeatPricePerKgBdt.Should().Be(750m, "still cached");

        provider.Invalidate(TenantA);
        provider.Current.DefaultMeatPricePerKgBdt.Should().Be(900m);
        _repository.Loads.Should().Be(2);
    }

    // ── Job scope ───────────────────────────────────────────────────────────

    [Fact]
    public void Job_scope_supplies_the_tenant_when_there_is_no_request_tenant()
    {
        _repository.Rows.Add(TenantBusinessSettings.Create(TenantA, null, new BusinessRulesOverrides(DefaultMeatPricePerKgBdt: 750m)));
        _repository.Rows.Add(TenantBusinessSettings.Create(TenantB, null, new BusinessRulesOverrides(DefaultMeatPricePerKgBdt: 500m)));
        var provider = ProviderFor(Guid.Empty);

        provider.Current.DefaultMeatPricePerKgBdt.Should().Be(680m);

        using (provider.BeginTenantScope(TenantA))
        {
            provider.Current.DefaultMeatPricePerKgBdt.Should().Be(750m);

            using (provider.BeginTenantScope(TenantB))
                provider.Current.DefaultMeatPricePerKgBdt.Should().Be(500m);

            provider.Current.DefaultMeatPricePerKgBdt.Should().Be(750m, "disposing the inner scope restores the outer one");
        }

        provider.Current.DefaultMeatPricePerKgBdt.Should().Be(680m);
    }

    [Fact]
    public async Task Job_scope_flows_across_awaits_and_wins_over_the_request_tenant()
    {
        _repository.Rows.Add(TenantBusinessSettings.Create(TenantB, null, new BusinessRulesOverrides(DefaultMeatPricePerKgBdt: 500m)));
        var provider = ProviderFor(TenantA);

        using (provider.BeginTenantScope(TenantB))
        {
            await Task.Yield();
            provider.Current.DefaultMeatPricePerKgBdt.Should().Be(500m);
        }

        provider.Current.DefaultMeatPricePerKgBdt.Should().Be(680m);
    }

    [Fact]
    public void Provider_is_scoped_and_cache_singleton_without_capturing_scoped_services()
    {
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddOptions();
        services.AddSingleton<ITenantBusinessSettingsRepository>(_repository);
        services.AddScoped(_ => Substitute.For<ITenantService>());
        services.AddSingleton<FarmBusinessRulesCache>();
        services.AddScoped<IFarmBusinessRulesProvider, FarmBusinessRulesProvider>();

        using var root = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = root.CreateScope();

        scope.ServiceProvider.GetRequiredService<IFarmBusinessRulesProvider>().Current.Should().NotBeNull();
        var act = () => root.GetRequiredService<IFarmBusinessRulesProvider>();
        act.Should().Throw<InvalidOperationException>("the provider must not be resolvable as a singleton");
    }

    // ── Save path ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Saving_creates_the_row_and_invalidates_the_cache()
    {
        var tenant = Substitute.For<ITenantService>();
        tenant.TenantId.Returns(TenantA);
        var provider = new FarmBusinessRulesProvider(_cache, tenant);
        provider.Current.DefaultMeatPricePerKgBdt.Should().Be(680m); // primes the cache

        var save = new SaveBusinessRulesSettingsCommandHandler(_repository, provider, tenant, Substitute.For<IUnitOfWork>());
        var dto = await save.Handle(
            new SaveBusinessRulesSettingsCommand(null, new BusinessRulesOverrides(DefaultMeatPricePerKgBdt: 720m)),
            CancellationToken.None);

        dto.Effective.DefaultMeatPricePerKgBdt.Should().Be(720m);
        dto.Inherited.DefaultMeatPricePerKgBdt.Should().Be(680m);
        dto.IsCustomized.Should().BeTrue();
        provider.Current.DefaultMeatPricePerKgBdt.Should().Be(720m);

        // Reset to defaults = all fields null.
        dto = await save.Handle(new SaveBusinessRulesSettingsCommand(null, new BusinessRulesOverrides()), CancellationToken.None);
        dto.IsCustomized.Should().BeFalse();
        provider.Current.DefaultMeatPricePerKgBdt.Should().Be(680m);
        _repository.Rows.Should().ContainSingle();
    }

    // ── Validation ──────────────────────────────────────────────────────────

    private static readonly SaveBusinessRulesSettingsCommandValidator Validator = new();

    private static SaveBusinessRulesSettingsCommand Cmd(BusinessRulesOverrides values) => new(null, values);

    [Fact]
    public void Validator_accepts_a_full_valid_set_and_an_empty_reset()
    {
        Validator.TestValidate(Cmd(new BusinessRulesOverrides(
            FeedAllocationMethod.LiveWeight, 0.75d, GroupPlanQuantityBasis.WholeGroup, 150m,
            OverheadAllocationMethod.PerLiveWeight, OverheadCostPeriod.TransactionDate, AnimalValuationPolicy.LiveWeightMarket,
            0.55m, 700m, 420m, ReportLanguagePreference.Bangla, true))).ShouldNotHaveAnyValidationErrors();

        Validator.TestValidate(Cmd(new BusinessRulesOverrides())).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0.29)]
    [InlineData(0.71)]
    public void Validator_rejects_dressing_outside_30_to_70_percent(double dressing)
    {
        Validator.TestValidate(Cmd(new BusinessRulesOverrides(DefaultDressingPercentage: (decimal)dressing)))
            .ShouldHaveValidationErrorFor(c => c.Values.DefaultDressingPercentage);
    }

    [Fact]
    public void Validator_rejects_non_positive_prices_bad_exponent_and_unknown_enums()
    {
        var result = Validator.TestValidate(Cmd(new BusinessRulesOverrides(
            DefaultMeatPricePerKgBdt: 0m,
            DefaultLiveWeightPricePerKgBdt: -1m,
            MetabolicWeightExponent: 2d,
            Valuation: (AnimalValuationPolicy)99,
            FeedAllocation: (FeedAllocationMethod)42)));

        result.ShouldHaveValidationErrorFor(c => c.Values.DefaultMeatPricePerKgBdt);
        result.ShouldHaveValidationErrorFor(c => c.Values.DefaultLiveWeightPricePerKgBdt);
        result.ShouldHaveValidationErrorFor(c => c.Values.MetabolicWeightExponent);
        result.ShouldHaveValidationErrorFor(c => c.Values.Valuation);
        result.ShouldHaveValidationErrorFor(c => c.Values.FeedAllocation);
    }

    private sealed class FakeSettingsRepository : ITenantBusinessSettingsRepository
    {
        public List<TenantBusinessSettings> Rows { get; } = [];
        public int Loads { get; private set; }

        public Task<TenantBusinessSettings?> GetAsync(Guid? farmId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Rows.FirstOrDefault(r => r.FarmId == farmId && r.TenantId == TenantA));

        public IReadOnlyList<TenantBusinessSettings> GetAllForTenant(Guid tenantId)
        {
            Loads++;
            return Rows.Where(r => r.TenantId == tenantId).ToList();
        }

        public void Add(TenantBusinessSettings settings) => Rows.Add(settings);
    }
}
