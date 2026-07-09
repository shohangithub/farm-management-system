using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Farm360.Persistence.Context;

/// <summary>
/// Design-time factory for ApplicationDbContext.
/// Required by EF Core tooling (dotnet ef migrations add ...).
/// NEVER used at runtime — only by the CLI toolchain.
/// Convention: reads connection string from appsettings.Development.json.
/// </summary>
public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        // Walk up to find the API project where appsettings lives
        var basePath = Path.Combine(Directory.GetCurrentDirectory());

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables(prefix: "FARM360_")
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Server=localhost,1433;Database=Farm360_Dev;User Id=sa;Password=Farm360_Dev!2026;TrustServerCertificate=True;";

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
            sqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
        });

        // Design-time: tenant context is not available — use a null-object implementation
        return new ApplicationDbContext(optionsBuilder.Options, new DesignTimeTenantService());
    }
}

/// <summary>
/// Null-object tenant service for design-time context creation.
/// Provides empty values so migrations can generate without a real tenant context.
/// </summary>
internal sealed class DesignTimeTenantService : Application.Common.Interfaces.ITenantService
{
    public Guid TenantId => Guid.Empty;
    public string TenantSlug => "design-time";
    public string TenantName => "Design Time";
    public string SubscriptionTier => "None";
    public string TenantStatus => "Active";
    public bool IsActive => true;
    public bool IsGracePeriod => false;
    public void SetTenant(Guid tenantId, string slug, string name, string tier, string status) { }
}
