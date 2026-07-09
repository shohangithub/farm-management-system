using Microsoft.AspNetCore.Identity;

namespace Farm360.Identity.Entities;

/// <summary>
/// Farm360 ApplicationUser — extends ASP.NET Core Identity IdentityUser.
/// F360-AUTH-2026-001 §2.1: Phone is the primary identity (not email).
/// Email is optional. PasswordHash is optional (OTP-first system).
/// TokenVersion: incremented to revoke ALL active JWT sessions instantly.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    // ── Core Identity ─────────────────────────────────────────────────────────
    // PhoneNumber, PhoneNumberConfirmed, Email, EmailConfirmed: inherited from IdentityUser
    // PasswordHash: nullable (OTP-first: no password required)
    // SecurityStamp: rotated on all security-sensitive changes

    // ── Token Revocation (F360-AUTH-2026-001 §3) ─────────────────────────────
    /// <summary>
    /// Revocation counter. Embedded in JWT tv claim.
    /// Increment → all existing access tokens immediately invalid (within 30 seconds via Redis cache).
    /// </summary>
    public int TokenVersion { get; set; } = 1;

    // ── System User ───────────────────────────────────────────────────────────
    /// <summary>True for internal platform admin accounts. Bypasses tenant isolation checks.</summary>
    public bool IsSystemUser { get; set; } = false;

    // ── Audit ─────────────────────────────────────────────────────────────────
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }

    /// <summary>HMAC-SHA256 of last login IP. Raw IP never stored. F360-AUTH-2026-001 §11.1</summary>
    public string? LastLoginIpHash { get; set; }

    // ── 2FA / TOTP (Phase 2 — F360-AUTH-2026-001 §15.5) ─────────────────────
    /// <summary>KMS-encrypted TOTP secret. Raw secret never stored in DB.</summary>
    public string? TotpSecretEncrypted { get; set; }

    /// <summary>JSON: last 5 password hashes (prevent reuse — F360-AUTH-2026-001 §4.5)</summary>
    public string? PasswordHistory { get; set; }
}
