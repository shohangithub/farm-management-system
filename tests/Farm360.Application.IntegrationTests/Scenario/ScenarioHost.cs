using Farm360.Application.Common.Interfaces;
using Farm360.Application.DependencyInjection;
using Farm360.Application.Intelligence.Interfaces;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Identity.Services;
using Farm360.Infrastructure.BusinessRules;
using Farm360.Infrastructure.Caching;
using Farm360.Infrastructure.Reporting.Csv;
using Farm360.Infrastructure.Reporting.Excel;
using Farm360.Infrastructure.Reporting.Pdf;
using Farm360.Persistence.DependencyInjection;
using Farm360.Persistence.Services;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Farm360.Application.IntegrationTests.Scenario;

/// <summary>
/// The application wired the way the API wires it (Application + Persistence layers, the identity
/// context services, the business-rules provider and the report renderers) against the test SQL
/// Server database, minus HTTP, Hangfire, SignalR and Redis. Every <see cref="SendAsync{T}"/> runs in
/// a fresh DI scope (fresh DbContext) acting as the tenant's owner — exactly like one API request.
/// </summary>
internal sealed class ScenarioHost : IAsyncDisposable
{
    private const string OwnerRole = "Owner";

    /// <summary>The scenario's "today" (the farm clock every handler and validator reads).</summary>
    public static readonly DateOnly Today = new(2026, 6, 30);

    private readonly ServiceProvider _root;

    private readonly string _connectionString;

    private ScenarioHost(ServiceProvider root, Guid userId, string connectionString)
    {
        _root = root;
        UserId = userId;
        _connectionString = connectionString;
    }

    public Guid UserId { get; }

    public Guid TenantId { get; private set; }

    public Guid OrganizationId { get; private set; }

    /// <summary>A second view of the same tenant with the farm rule "carry growing costs" set as given.</summary>
    public ScenarioHost WithCarryGrowingCosts(bool carry) =>
        new(BuildProvider(_connectionString, carry), UserId, _connectionString)
        {
            TenantId = TenantId,
            OrganizationId = OrganizationId,
        };

    public static async Task<ScenarioHost> CreateAsync(string connectionString)
    {
        var root = BuildProvider(connectionString, carryGrowingCosts: true);
        var host = new ScenarioHost(root, Guid.NewGuid(), connectionString);

        await using (var scope = root.CreateAsyncScope())
        {
            await SeedOnceAsync(scope.ServiceProvider, connectionString);
            var memberships = scope.ServiceProvider.GetRequiredService<TenantMembershipService>();
            var slug = "it-" + Guid.NewGuid().ToString("N")[..12];
            host.TenantId = await memberships.CreateTenantForUserAsync(
                host.UserId, "Scenario Farm Ltd", slug, "owner@scenario.test", "+8801700000000", 7, CancellationToken.None);
        }

        host.OrganizationId = await host.InScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<Farm360.Persistence.Context.ApplicationDbContext>();
            var tenant = db.Tenants.Single(t => t.Id == host.TenantId);
            tenant.Subscribe(Farm360.Domain.Tenancy.SubscriptionTier.Professional, Farm360.Domain.Tenancy.SubscriptionBillingCycle.OneTime);
            await db.SaveChangesAsync();
            return db.Organizations.Where(o => o.TenantId == host.TenantId).Select(o => o.Id).Single();
        });

        return host;
    }

    private static readonly SemaphoreSlim SeedGate = new(1, 1);
    private static readonly HashSet<string> Seeded = new(StringComparer.Ordinal);

    /// <summary>System roles, permissions and the breed catalogue, as the API seeds them at start-up.</summary>
    private static async Task SeedOnceAsync(IServiceProvider services, string connectionString)
    {
        await SeedGate.WaitAsync();
        try
        {
            if (Seeded.Add(connectionString))
                await services.GetRequiredService<Farm360.Persistence.Seed.DataSeeder>().SeedAsync(includeDemoData: false);
        }
        finally
        {
            SeedGate.Release();
        }
    }

    private static ServiceProvider BuildProvider(string connectionString, bool carryGrowingCosts)
    {
        var section = Farm360.Domain.BusinessRules.FarmBusinessRules.SectionName;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                [$"{section}:CarryLivestockGrowingCosts"] = carryGrowingCosts ? "true" : "false",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddHttpContextAccessor();

        services.AddApplicationServices();
        services.AddPersistenceServices(configuration);

        // Identity: the request's tenant and user (set per scope, as the demo seeder does).
        services.AddScoped<CurrentUserOverride>();
        services.AddScoped<ICurrentUserOverride>(sp => sp.GetRequiredService<CurrentUserOverride>());
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ITenantService, TenantService>();
        services.AddSingleton<IDateTimeService, DateTimeService>();

        // Infrastructure the application needs, without Redis / Hangfire / SignalR.
        services.AddDistributedMemoryCache();
        services.AddScoped<ICacheService, RedisCacheService>();
        services.Configure<Farm360.Domain.BusinessRules.FarmBusinessRules>(
            configuration.GetSection(Farm360.Domain.BusinessRules.FarmBusinessRules.SectionName));
        services.AddMemoryCache();
        services.AddSingleton<FarmBusinessRulesCache>();
        services.AddScoped<IFarmBusinessRulesProvider, FarmBusinessRulesProvider>();
        Farm360.Infrastructure.Reporting.ReportFonts.EnsureRegistered();
        services.AddSingleton<IReportRenderer, QuestPdfReportRenderer>();
        services.AddSingleton<IReportRenderer, ClosedXmlReportRenderer>();
        services.AddSingleton<IReportRenderer, CsvReportRenderer>();
        services.AddSingleton(Substitute.For<INotificationService>());
        services.AddSingleton(Substitute.For<IBackgroundJobService>());
        services.AddSingleton(Substitute.For<IIntelligenceEventChannel>());
        services.AddSingleton(Substitute.For<IEmailService>());
        services.AddSingleton(Substitute.For<ISmsService>());
        services.AddSingleton(Substitute.For<IFileStorageService>());
        var profiles = Substitute.For<IUserProfileService>();
        profiles.GetAsync(default, default).ReturnsForAnyArgs(new UserProfileDetails("Scenario Owner", "owner@scenario.test", null));
        services.AddSingleton(profiles);

        // The owner may run everything (permission rows are seeded by the identity seeder, not here).
        var permissions = Substitute.For<IPermissionService>();
        permissions.HasPermissionAsync(default, default, default!, default).ReturnsForAnyArgs(true);
        permissions.GetPermissionsAsync(default, default, default).ReturnsForAnyArgs(Array.Empty<string>());
        services.AddSingleton(permissions);

        // "Today" is the last day of the scenario's fiscal year, so "this year" on the dashboard is that year.
        services.AddScoped<IFarmClock>(_ => new FixedFarmClock(Today));

        return services.BuildServiceProvider();
    }

    /// <summary>Runs <paramref name="work"/> in a fresh scope acting as the tenant's owner.</summary>
    public async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        await using var scope = _root.CreateAsyncScope();
        var services = scope.ServiceProvider;
        services.GetRequiredService<ITenantService>().SetTenant(TenantId, "scenario", "Scenario Farm Ltd", "Professional", "Active");
        if (TenantId != Guid.Empty)
            services.GetRequiredService<ICurrentUserOverride>().Set(UserId, TenantId, OwnerRole, "Professional");
        return await work(services);
    }

    public Task InScopeAsync(Func<IServiceProvider, Task> work) =>
        InScopeAsync<bool>(async sp =>
        {
            await work(sp);
            return true;
        });

    public Task<T> SendAsync<T>(IRequest<T> request) =>
        InScopeAsync(sp => sp.GetRequiredService<ISender>().Send(request));

    public Task SendAsync(IRequest request) =>
        InScopeAsync(sp => sp.GetRequiredService<ISender>().Send(request));

    public ValueTask DisposeAsync() => _root.DisposeAsync();
}

/// <summary>A farm clock stopped on one day (UTC zone).</summary>
internal sealed class FixedFarmClock(DateOnly today) : IFarmClock
{
    public DateTime UtcNow => today.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc);

    public TimeZoneInfo TimeZone => TimeZoneInfo.Utc;

    public DateOnly Today => today;

    public DateTime Now => UtcNow;

    public DateOnly TodayFor(Guid tenantId) => today;

    public DateOnly TodayForFarm(Guid tenantId, Guid farmId) => today;

    public TimeZoneInfo ZoneFor(Guid tenantId, Guid? farmId = null) => TimeZoneInfo.Utc;
}
