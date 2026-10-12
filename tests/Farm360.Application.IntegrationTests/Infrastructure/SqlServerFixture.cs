using Farm360.Application.Common.Interfaces;
using Farm360.Persistence.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Xunit;

namespace Farm360.Application.IntegrationTests.Infrastructure;

/// <summary>
/// Real SQL Server for integration tests, migrated with the app's actual migrations.
/// <para>
/// Source, in order: the <c>FARM360_TEST_SQL</c> environment variable (a server-level connection
/// string, e.g. <c>Server=.;Integrated Security=True;TrustServerCertificate=True</c>), else a
/// Testcontainers SQL Server in Docker. Either way the tests run in a freshly created, uniquely
/// named database that is dropped afterwards -- they never touch an existing database.
/// </para>
/// </summary>
#pragma warning disable CA1515 // xUnit needs the fixture and collection definition public: they appear in public test-class constructors.
public sealed class SqlServerFixture : IAsyncLifetime
{
    public const string ConnectionStringVariable = "FARM360_TEST_SQL";

    private MsSqlContainer? _container;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        // xUnit can still build collection fixtures when every test in the collection is skipped.
        if (!SqlServerAvailability.IsAvailable)
        {
            return;
        }

        var serverConnectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        if (string.IsNullOrWhiteSpace(serverConnectionString))
        {
            _container = new MsSqlBuilder().Build();
            await _container.StartAsync();
            serverConnectionString = _container.GetConnectionString();
        }

        ConnectionString = new SqlConnectionStringBuilder(serverConnectionString)
        {
            InitialCatalog = $"Farm360_IT_{Guid.NewGuid():N}",
        }.ConnectionString;

        await using var context = CreateContext(new TestTenantService());
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrEmpty(ConnectionString))
        {
            return;
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
            return;
        }

        await using var context = CreateContext(new TestTenantService());
        await context.Database.EnsureDeletedAsync();
    }

    /// <summary>Mirrors the app's registration (PersistenceServiceExtensions), minus the audit interceptor.</summary>
    public ApplicationDbContext CreateContext(ITenantService tenantService)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(ConnectionString, sql => sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName))
            .Options;

        return new ApplicationDbContext(options, tenantService);
    }
}

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SqlServer";
}
#pragma warning restore CA1515

/// <summary>
/// A test that needs SQL Server. Skipped -- not failed -- when neither the FARM360_TEST_SQL
/// variable nor a Docker engine is available, so <c>dotnet test</c> stays green on machines
/// that can't host a database.
/// </summary>
internal sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (!SqlServerAvailability.IsAvailable)
        {
            Skip = $"No SQL Server: set {SqlServerFixture.ConnectionStringVariable} or start Docker.";
        }
    }
}

internal static class SqlServerAvailability
{
    public static bool IsAvailable { get; } =
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SqlServerFixture.ConnectionStringVariable))
        || DockerIsAvailable();

    private static bool DockerIsAvailable()
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOCKER_HOST")))
        {
            return true;
        }

        if (OperatingSystem.IsWindows())
        {
            return Directory.GetFiles(@"\\.\pipe\").Any(p => p.EndsWith("docker_engine", StringComparison.OrdinalIgnoreCase));
        }

        return File.Exists("/var/run/docker.sock");
    }
}

/// <summary>Settable tenant context, standing in for the per-request TenantResolutionMiddleware.</summary>
internal sealed class TestTenantService : ITenantService
{
    public Guid TenantId { get; private set; }
    public string TenantSlug { get; private set; } = string.Empty;
    public string TenantName { get; private set; } = string.Empty;
    public string SubscriptionTier { get; private set; } = "Starter";
    public string TenantStatus { get; private set; } = "Active";
    public bool IsActive => TenantStatus == "Active";
    public bool IsGracePeriod => TenantStatus == "GracePeriod";

    public void SetTenant(Guid tenantId, string slug, string name, string tier, string status)
    {
        TenantId = tenantId;
        TenantSlug = slug;
        TenantName = name;
        SubscriptionTier = tier;
        TenantStatus = status;
    }

    public static TestTenantService For(Guid tenantId)
    {
        var service = new TestTenantService();
        service.SetTenant(tenantId, "test", "Test Tenant", "Starter", "Active");
        return service;
    }
}
