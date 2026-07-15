using Farm360.Application.Common.Interfaces;
using Farm360.Identity.Context;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace Farm360.Identity.Services;

/// <summary>
/// OTP generation and verification service.
/// F360-AUTH-2026-001 §4 (OTP Authentication):
///   - 6-digit numeric OTP
///   - Redis is PRIMARY store (5 min TTL)
///   - Max 3 attempts per OTP session. Lockout: 15 minutes.
///   - OTP values are NEVER logged (only masked phone).
///   - OtpVerification DB record written for audit trail only.
/// </summary>
public sealed class OtpService(
    ICacheService cache,
    ISmsService smsService,
    IdentityDbContext identityContext,
    ILogger<OtpService> logger)
    : IOtpService
{
    private static readonly TimeSpan OtpTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private const int MaxAttempts = 3;

    public async Task<string> GenerateAndSendAsync(
        string phoneNumber,
        OtpPurpose purpose,
        CancellationToken cancellationToken = default)
    {
        // Check lockout before generating new OTP
        if (await IsLockedOutAsync(phoneNumber, cancellationToken))
            throw new InvalidOperationException($"Phone {MaskPhone(phoneNumber)} is locked out due to too many failed OTP attempts.");

        // Generate cryptographically random 6-digit code
        var otp = GenerateOtp();
        var otpKey = BuildOtpKey(phoneNumber, purpose);
        var attemptsKey = BuildAttemptsKey(phoneNumber, purpose);

        // Store OTP in Redis (primary store)
        await cache.SetAsync(otpKey, otp, OtpTtl, cancellationToken);

        // Reset attempt counter on new OTP generation
        await cache.RemoveAsync(attemptsKey, cancellationToken);

        // Send via SMS — OTP value NOT logged
        await smsService.SendOtpAsync(phoneNumber, otp, purpose.ToString(), cancellationToken);

        // Write audit record to DB (no OTP value stored)
        var auditRecord = new OtpVerification
        {
            Id = Guid.NewGuid(),
            PhoneMasked = MaskPhone(phoneNumber),
            Purpose = (byte)purpose,
            AttemptCount = 0,
            IsVerified = false,
            ExpiresAt = DateTime.UtcNow.Add(OtpTtl),
            CreatedAt = DateTime.UtcNow
        };

        identityContext.OtpVerifications.Add(auditRecord);
        await identityContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Farm360 OTP: Generated for Phone={PhoneMasked} Purpose={Purpose}",
            MaskPhone(phoneNumber), purpose);

        return otp; // Returned to caller only for testing. NEVER log this value.
    }

    public async Task<bool> VerifyAsync(
        string phoneNumber,
        string otpCode,
        OtpPurpose purpose,
        CancellationToken cancellationToken = default)
    {
        var otpKey = BuildOtpKey(phoneNumber, purpose);
        var attemptsKey = BuildAttemptsKey(phoneNumber, purpose);
        var lockoutKey = BuildLockoutKey(phoneNumber);

        // Check lockout
        if (await IsLockedOutAsync(phoneNumber, cancellationToken))
        {
            logger.LogWarning("Farm360 OTP: Verification attempted while locked out. Phone={PhoneMasked}", MaskPhone(phoneNumber));
            return false;
        }

        var storedOtp = await cache.GetAsync<string>(otpKey, cancellationToken);

        if (storedOtp is null)
        {
            logger.LogWarning("Farm360 OTP: Expired or not found for Phone={PhoneMasked}", MaskPhone(phoneNumber));
            return false;
        }

        // Increment attempt counter regardless of result
        var attempts = (await cache.GetAsync<int?>(attemptsKey, cancellationToken) ?? 0) + 1;
        await cache.SetAsync(attemptsKey, attempts, OtpTtl, cancellationToken);

        if (!string.Equals(storedOtp, otpCode, StringComparison.Ordinal))
        {
            if (attempts >= MaxAttempts)
            {
                // Lock out the phone number
                await cache.SetAsync(lockoutKey, true, LockoutDuration, cancellationToken);
                await cache.RemoveAsync(otpKey, cancellationToken);
                logger.LogWarning("Farm360 OTP: LOCKED OUT Phone={PhoneMasked} after {Attempts} failed attempts", MaskPhone(phoneNumber), attempts);
            }
            else
            {
                logger.LogWarning("Farm360 OTP: Invalid code for Phone={PhoneMasked} Attempt={Attempt}/{Max}", MaskPhone(phoneNumber), attempts, MaxAttempts);
            }
            return false;
        }

        // Success — remove OTP from cache immediately
        await cache.RemoveAsync(otpKey, cancellationToken);
        await cache.RemoveAsync(attemptsKey, cancellationToken);

        logger.LogInformation("Farm360 OTP: Verified successfully for Phone={PhoneMasked} Purpose={Purpose}", MaskPhone(phoneNumber), purpose);
        return true;
    }

    public async Task<bool> IsLockedOutAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        var lockoutKey = BuildLockoutKey(phoneNumber);
        return await cache.GetAsync<bool?>(lockoutKey, cancellationToken) == true;
    }

    // ── Private helpers ───────────────────────────────────────────────────────
    private static string GenerateOtp()
    {
        // Cryptographically random 6-digit number
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[4];
        rng.GetBytes(bytes);
        var value = BitConverter.ToUInt32(bytes, 0) % 1_000_000;
        return value.ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string BuildOtpKey(string phoneNumber, OtpPurpose purpose)
        => $"otp:{HashPhone(phoneNumber)}:{purpose}";

    private static string BuildAttemptsKey(string phoneNumber, OtpPurpose purpose)
        => $"otp:attempts:{HashPhone(phoneNumber)}:{purpose}";

    private static string BuildLockoutKey(string phoneNumber)
        => $"otp:lockout:{HashPhone(phoneNumber)}";

    private static string HashPhone(string phone)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(phone);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash)[..16]; // 8 bytes = sufficient for cache key
    }

    private static string MaskPhone(string phone)
    {
        if (phone.Length < 5) return "***";
        return phone[..3] + "***" + phone[^2..];
    }
}
