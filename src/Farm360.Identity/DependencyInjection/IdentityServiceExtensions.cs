using Farm360.Application.Common.Interfaces;
using Farm360.Identity.Configuration;
using Farm360.Identity.Context;
using Farm360.Identity.Entities;
using Farm360.Identity.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Farm360.Identity.DependencyInjection;

/// <summary>
/// Identity layer DI registration.
/// F360-AUTH-2026-001: ASP.NET Core Identity + JWT HS256 + OTP + Refresh tokens + Permission-based AuthZ.
/// </summary>
public static class IdentityServiceExtensions
{
    public static IServiceCollection AddIdentityServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── Jwt Configuration ─────────────────────────────────────────────────
        services.Configure<JwtConfiguration>(configuration.GetSection(JwtConfiguration.SectionName));
        var jwtConfig = configuration.GetSection(JwtConfiguration.SectionName).Get<JwtConfiguration>()
            ?? throw new InvalidOperationException("Jwt configuration section is missing.");

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

            options.SignIn.RequireConfirmedPhoneNumber = false; // Enforced in auth flow, not here
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<IdentityDbContext>()
        .AddDefaultTokenProviders();

        // ── JWT Authentication ────────────────────────────────────────────────
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtConfig.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtConfig.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtConfig.Secret)),
                    ClockSkew = TimeSpan.FromSeconds(30), // F360-AUTH-2026-001 §16 R-12
                };

                // SignalR JWT from query string (F360-AUTH-2026-001 §5)
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = ctx =>
                    {
                        var accessToken = ctx.Request.Query["access_token"];
                        var path = ctx.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) &&
                            path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase))
                        {
                            ctx.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    },
                };
            });

        // ── Permission-based Authorization (handlers registered at API layer) ───
        // Note: PermissionPolicyProvider + PermissionHandler live in Farm360.Api
        // and are registered by ApiServiceExtensions to avoid circular references.
        services.AddAuthorization();

        // ── Application Service Implementations ───────────────────────────────
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ITenantService, TenantService>();
        services.AddSingleton<IDateTimeService, DateTimeService>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<IOtpService, OtpService>();

        return services;
    }
}
