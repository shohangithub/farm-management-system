using Farm360.Identity.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Farm360.Identity.Seed;

/// <summary>
/// Seeds the platform (system) administrator account.
/// <list type="bullet">
///   <item>Development: uses <c>Seed:SystemAdmin:Phone</c>/<c>Seed:SystemAdmin:Password</c> when
///   configured, otherwise the well-known local development credentials.</item>
///   <item>Any other environment: seeds ONLY when both values are supplied through configuration
///   (environment variables / secret store). There is no default password outside Development.</item>
/// </list>
/// An admin user that already exists is never modified (password, flags and claims are untouched).
/// </summary>
public sealed class IdentitySeeder(
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<IdentitySeeder> logger)
{
    private const string DevelopmentAdminPhone = "+8801806580501";
    private const string DevelopmentAdminPassword = "Password123!";
    private static readonly Guid SeededAdminId = new("11111111-1111-1111-1111-111111111111");

    public async Task SeedAsync()
    {
        var (phone, password) = ResolveCredentials();
        if (phone is null || password is null)
        {
            logger.LogInformation(
                "Farm360 IdentitySeeder: No system admin credentials configured (Seed:SystemAdmin:Phone/Password) " +
                "for environment {Environment}. Skipping admin seed.", environment.EnvironmentName);
            return;
        }

        var existingUser = await userManager.FindByNameAsync(phone)
            ?? await userManager.FindByIdAsync(SeededAdminId.ToString());
        if (existingUser != null)
        {
            logger.LogInformation("Farm360 IdentitySeeder: Admin user already exists. Skipping.");
            return;
        }

        logger.LogInformation("Farm360 IdentitySeeder: Admin user not found. Creating system admin...");

        var adminUser = new ApplicationUser
        {
            Id = SeededAdminId,
            UserName = phone,
            PhoneNumber = phone,
            Email = configuration["Seed:SystemAdmin:Email"] ?? "admin@farm360.ai",
            IsSystemUser = true,
            EmailConfirmed = true,
            PhoneNumberConfirmed = true
        };

        var result = await userManager.CreateAsync(adminUser, password);
        if (result.Succeeded)
        {
            logger.LogInformation("Farm360 IdentitySeeder: System admin user created successfully.");
        }
        else
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            logger.LogError("Farm360 IdentitySeeder: Failed to create admin user: {Errors}", errors);
        }
    }

    internal (string? Phone, string? Password) ResolveCredentials()
    {
        var phone = configuration["Seed:SystemAdmin:Phone"];
        var password = configuration["Seed:SystemAdmin:Password"];

        if (!string.IsNullOrWhiteSpace(phone) && !string.IsNullOrWhiteSpace(password))
        {
            return (phone.Trim(), password);
        }

        if (environment.IsDevelopment())
        {
            return (DevelopmentAdminPhone, DevelopmentAdminPassword);
        }

        return (null, null);
    }
}
