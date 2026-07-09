using Farm360.Application.Common.Interfaces;
using Farm360.Identity.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Identity.Context;

/// <summary>
/// Identity DbContext — separate from ApplicationDbContext (business data).
/// F360-AUTH-2026-001 §11: All auth tables in identity.* schema.
/// Contains: Users, Sessions, Devices, OtpVerifications, ExternalProviders, AuthAuditLogs.
/// Global query filters: NOT applied here (identity data is cross-tenant by design for platform admins).
/// </summary>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : IdentityDbContext<ApplicationUser, Microsoft.AspNetCore.Identity.IdentityRole<Guid>, Guid>(options)
{
    // ── Session Management (F360-AUTH-2026-001 §5) ──────────────────────────
    public DbSet<UserSession> UserSessions => Set<UserSession>();

    // ── Remember Device (F360-AUTH-2026-001 §6) ─────────────────────────────
    public DbSet<UserDevice> UserDevices => Set<UserDevice>();

    // ── OTP Tracking (audit trail — Redis is primary store) ─────────────────
    public DbSet<OtpVerification> OtpVerifications => Set<OtpVerification>();

    // ── Auth Audit Log (INSERT only — never UPDATE or DELETE) ────────────────
    public DbSet<AuthAuditLog> AuthAuditLogs => Set<AuthAuditLog>();

    // ── External Providers (Phase 2: Google, Microsoft, Facebook) ────────────
    public DbSet<ExternalProvider> ExternalProviders => Set<ExternalProvider>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Use identity.* schema for all identity tables
        builder.HasDefaultSchema("identity");

        // Rename ASP.NET Identity default tables to PascalCase
        builder.Entity<ApplicationUser>().ToTable("Users");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityRole<Guid>>().ToTable("Roles");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>>().ToTable("UserRoles");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserClaim<Guid>>().ToTable("UserClaims");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<Guid>>().ToTable("UserLogins");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityRoleClaim<Guid>>().ToTable("RoleClaims");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>>().ToTable("UserTokens");

        // Apply configurations from assembly
        builder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
    }
}

// ── Session entity ────────────────────────────────────────────────────────────
/// <summary>Refresh token store. F360-AUTH-2026-001 §5.1</summary>
public sealed class UserSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public string TokenHash { get; set; } = string.Empty; // HMAC-SHA256 of raw refresh token
    public Guid? DeviceId { get; set; }
    public string? DeviceName { get; set; }
    public byte DeviceType { get; set; } // 0=Browser, 1=Mobile, 2=Desktop
    public string? DeviceFingerprint { get; set; }
    public string? IpHash { get; set; }
    public string? UserAgent { get; set; }
    public string? Location { get; set; }
    public bool IsHighRisk { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime LastUsedAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime? RevokedAt { get; set; }
    public byte? RevokedReason { get; set; } // 0=Logout, 1=PasswordChange, 2=Admin, 3=Suspicious
    public Guid? ReplacedBySessionId { get; set; }
    public ApplicationUser? User { get; set; }
}

/// <summary>Remember Device store. F360-AUTH-2026-001 §6</summary>
public sealed class UserDevice
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public string DeviceFingerprint { get; set; } = string.Empty;
    public string? DeviceName { get; set; }
    public byte DeviceType { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime LastUsedAt { get; set; }
    public bool IsRevoked { get; set; }
    public ApplicationUser? User { get; set; }
}

/// <summary>OTP verification audit record. Redis is primary store.</summary>
public sealed class OtpVerification
{
    public Guid Id { get; set; }
    public string PhoneMasked { get; set; } = string.Empty;
    public byte Purpose { get; set; } // 0=Register, 1=Login, 2=PwdReset, 3=MFA
    public byte AttemptCount { get; set; }
    public bool IsVerified { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Immutable auth audit log. INSERT ONLY. F360-AUTH-2026-001 §10</summary>
public sealed class AuthAuditLog
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public byte Severity { get; set; } // 0=Info, 1=Warning, 2=Alert, 3=Critical
    public Guid? UserId { get; set; }
    public Guid? TenantId { get; set; }
    public string? PhoneMasked { get; set; }
    public string? EmailMasked { get; set; }
    public string? IpHash { get; set; }
    public string? UserAgent { get; set; }
    public Guid? DeviceId { get; set; }
    public Guid? SessionId { get; set; }
    public string? Location { get; set; }
    public string? CorrelationId { get; set; }
    public string? AdditionalData { get; set; } // JSON
    public DateTime OccurredAt { get; set; }
}

/// <summary>External OAuth provider links (Phase 2). F360-AUTH-2026-001 §15</summary>
public sealed class ExternalProvider
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Provider { get; set; } = string.Empty; // Google, Microsoft, Facebook
    public string ProviderSubjectId { get; set; } = string.Empty;
    public string? ProviderEmail { get; set; }
    public string? AccessTokenEncrypted { get; set; } // KMS-encrypted
    public DateTime LinkedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public ApplicationUser? User { get; set; }
}
