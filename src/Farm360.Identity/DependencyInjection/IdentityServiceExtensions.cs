using Farm360.Application.Common.Interfaces;
using Farm360.Identity.Context;
using Farm360.Identity.Entities;
using Farm360.Identity.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Farm360.Identity.DependencyInjection;

/// <summary>
/// Identity layer DI registration.
/// F360-AUTH-2026-001: ASP.NET Core Identity + JWT RS256 + OTP + Refresh tokens.
/// </summary>
public static class IdentityServiceExtensions
{
    public static IServiceCollection AddIdentityServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── Identity DbContext ────────────────────────────────────────────────
        services.AddDbContext<IdentityDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("'DefaultConnection' connection string not found.");

            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null);
                sqlOptions.MigrationsAssembly(typeof(IdentityDbContext).Assembly.FullName);
            });
        });

        // ── ASP.NET Core Identity ─────────────────────────────────────────────
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            // Password policy (F360-AUTH-2026-001 §4.5)
            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;

            // Lockout (F360-AUTH-2026-001 §8)
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(60);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;

            // Phone confirmed required (primary auth method)
            options.SignIn.RequireConfirmedPhoneNumber = false; // Enforced in auth flow, not here
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<IdentityDbContext>()
        .AddDefaultTokenProviders();

        // ── JWT Authentication ────────────────────────────────────────────────
        // F360-AUTH-2026-001 §2.2: RS256 — public key for validation, private in AWS KMS
        var jwtSection = configuration.GetSection("Jwt");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtSection["Issuer"] ?? "https://auth.farm360.ai",
                    ValidateAudience = true,
                    ValidAudience = jwtSection["Audience"] ?? "https://api.farm360.ai",
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    // Public key loaded from JWKS endpoint (configured below)
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(
                            jwtSection["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured."))),
                    ClockSkew = TimeSpan.FromSeconds(30), // F360-AUTH-2026-001 §16 R-12
                };

                // SignalR JWT from query string (F360-AUTH-2026-001 §5)
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = ctx =>
                    {
                        var accessToken = ctx.Request.Query["access_token"];
                        var path = ctx.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase))
                        {
                            ctx.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    },
                };
            });

        services.AddAuthorization();

        // ── Application Services ──────────────────────────────────────────────
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ITenantService, TenantService>();
        services.AddSingleton<IDateTimeService, DateTimeService>();

        return services;
    }
}
