using Farm360.Identity.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Farm360.Identity.Seed;

public sealed class IdentitySeeder(
    UserManager<ApplicationUser> userManager,
    ILogger<IdentitySeeder> logger)
{
    public async Task SeedAsync()
    {
        logger.LogInformation("Farm360 IdentitySeeder: Checking if admin user exists...");

        var adminPhone = "+8801806580501";
        
        var existingUser = await userManager.FindByNameAsync(adminPhone);
        if (existingUser == null)
        {
            logger.LogInformation("Farm360 IdentitySeeder: Admin user not found. Creating default admin...");

            var adminUser = new ApplicationUser
            {
                Id = new Guid("11111111-1111-1111-1111-111111111111"),
                UserName = adminPhone,
                PhoneNumber = adminPhone,
                Email = "admin@farm360.ai",
                IsSystemUser = true,
                EmailConfirmed = true,
                PhoneNumberConfirmed = true
            };

            var result = await userManager.CreateAsync(adminUser, "Password123!");
            if (result.Succeeded)
            {
                logger.LogInformation("Farm360 IdentitySeeder: Default admin user created successfully.");
            }
            else
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                logger.LogError("Farm360 IdentitySeeder: Failed to create admin user: {Errors}", errors);
            }
        }
        else
        {
            logger.LogInformation("Farm360 IdentitySeeder: Admin user already exists. Skipping.");
        }
    }
}
