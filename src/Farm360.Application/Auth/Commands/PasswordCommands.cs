using Farm360.Application.Common.Interfaces;
using Farm360.Application.Common.Phones;
using Farm360.Application.Users;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Auth.Commands;

// ══════════════════════════════════════════════════════════════════════════════
// CHANGE PASSWORD (authenticated)
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Changes the caller's password, revokes every session (token version bump + refresh tokens) and
/// returns a fresh token pair for the current device, so only this device stays signed in.
/// </summary>
public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword) : IRequest<LoginResponse>;

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).MeetsPasswordPolicy();
        RuleFor(x => x.NewPassword).NotEqual(x => x.CurrentPassword)
            .WithMessage("The new password must be different from the current one.");
    }
}

public sealed class ChangePasswordCommandHandler(
    ICurrentUserService currentUser,
    IUserAccountService accounts,
    IRefreshTokenService refreshTokens,
    IAuthService authService) : IRequestHandler<ChangePasswordCommand, LoginResponse>
{
    public async Task<LoginResponse> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("User is not authenticated.");

        await accounts.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword, cancellationToken);
        await refreshTokens.RevokeAllSessionsAsync(userId, SessionRevokeReason.PasswordChange, cancellationToken);

        var preferredTenant = currentUser.TenantId is { } t && t != Guid.Empty ? t : (Guid?)null;
        return await authService.IssueSessionAsync(userId, preferredTenant, cancellationToken);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// FORGOT / RESET PASSWORD via SMS OTP (anonymous)
//   1. request  → OTP sent if the phone has an account (response is always the same)
//   2. verify   → OTP checked → short-lived single-use reset token
//   3. reset    → token + new password → password set, all sessions revoked
// ══════════════════════════════════════════════════════════════════════════════

public static class PasswordReset
{
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(15);

    public const string InvalidCodeMessage = "The code is invalid or has expired.";
    public const string InvalidTokenMessage = "This password reset session has expired. Please start again.";

    public static string CacheKey(string rawToken) => $"auth:pwdreset:{SecureTokens.Hash(rawToken)}";
}

public sealed record RequestPasswordResetCommand(string Phone) : IRequest;

public sealed class RequestPasswordResetCommandValidator : AbstractValidator<RequestPasswordResetCommand>
{
    public RequestPasswordResetCommandValidator()
    {
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(32);
    }
}

public sealed class RequestPasswordResetCommandHandler(
    IUserAccountService accounts,
    IOtpService otp,
    ILogger<RequestPasswordResetCommandHandler> logger,
    TimeSpan? minimumResponseTime = null) : IRequestHandler<RequestPasswordResetCommand>
{
    /// <summary>
    /// Every request takes at least this long, so an unknown phone (which returns at once) cannot be
    /// told apart from a real account (which sends an SMS) by timing the response.
    /// </summary>
    public static readonly TimeSpan DefaultMinimumResponseTime = TimeSpan.FromSeconds(2);

    public async Task Handle(RequestPasswordResetCommand request, CancellationToken cancellationToken)
    {
        var started = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await SendIfAccountExistsAsync(request.Phone.Trim(), cancellationToken);
        }
        finally
        {
            var floor = minimumResponseTime ?? DefaultMinimumResponseTime;
            // Small jitter so the floor itself is not a fingerprint.
            var target = floor + TimeSpan.FromMilliseconds(System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, 250));
            var remaining = target - started.Elapsed;
            if (floor > TimeSpan.Zero && remaining > TimeSpan.Zero)
                await Task.Delay(remaining, CancellationToken.None);
        }
    }

    private async Task SendIfAccountExistsAsync(string typedPhone, CancellationToken cancellationToken)
    {
        // Same outcome whether or not the phone has an account (no user enumeration).
        // The lookup tries the canonical form and the typed one; the OTP is keyed on the canonical form.
        var account = await accounts.FindByPhoneAsync(typedPhone, cancellationToken);
        if (account is null) return;
        var phone = PhoneNumberNormalizer.Normalize(typedPhone) ?? typedPhone;

        if (await otp.IsLockedOutAsync(phone, cancellationToken))
        {
            logger.LogWarning("Farm360 PasswordReset: OTP requested while locked out for User={UserId}", account.UserId);
            return;
        }

        try
        {
            await otp.GenerateAndSendAsync(phone, OtpPurpose.PasswordReset, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            // Lockout raced in between; still answer generically.
            logger.LogWarning(ex, "Farm360 PasswordReset: OTP not sent for User={UserId}", account.UserId);
        }
    }
}

public sealed record VerifyPasswordResetCodeCommand(string Phone, string Code) : IRequest<PasswordResetTokenDto>;

public sealed record PasswordResetTokenDto(string ResetToken, int ExpiresInSeconds);

public sealed class VerifyPasswordResetCodeCommandValidator : AbstractValidator<VerifyPasswordResetCodeCommand>
{
    public VerifyPasswordResetCodeCommandValidator()
    {
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Code).NotEmpty().Length(6).Matches("^[0-9]{6}$").WithMessage("Enter the 6-digit code.");
    }
}

public sealed class VerifyPasswordResetCodeCommandHandler(
    IUserAccountService accounts,
    IOtpService otp,
    ICacheService cache) : IRequestHandler<VerifyPasswordResetCodeCommand, PasswordResetTokenDto>
{
    public async Task<PasswordResetTokenDto> Handle(VerifyPasswordResetCodeCommand request, CancellationToken cancellationToken)
    {
        var typedPhone = request.Phone.Trim();
        // Same canonical form the reset request keyed the OTP on.
        var phone = PhoneNumberNormalizer.Normalize(typedPhone) ?? typedPhone;

        // OTP lockout (3 wrong codes → 15 min) is enforced inside IOtpService.VerifyAsync.
        var verified = await otp.VerifyAsync(phone, request.Code.Trim(), OtpPurpose.PasswordReset, cancellationToken);
        var account = (verified ? await accounts.FindByPhoneAsync(typedPhone, cancellationToken) : null)
            ?? throw Fail.Validation(nameof(request.Code), PasswordReset.InvalidCodeMessage);

        var resetToken = SecureTokens.Generate();
        await cache.SetAsync(PasswordReset.CacheKey(resetToken), account.UserId.ToString(), PasswordReset.TokenLifetime, cancellationToken);

        return new PasswordResetTokenDto(resetToken, (int)PasswordReset.TokenLifetime.TotalSeconds);
    }
}

public sealed record ResetPasswordCommand(string ResetToken, string NewPassword) : IRequest;

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.ResetToken).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NewPassword).MeetsPasswordPolicy();
    }
}

public sealed class ResetPasswordCommandHandler(
    ICacheService cache,
    IUserAccountService accounts,
    IRefreshTokenService refreshTokens) : IRequestHandler<ResetPasswordCommand>
{
    public async Task Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var key = PasswordReset.CacheKey(request.ResetToken.Trim());
        var stored = await cache.GetAsync<string>(key, cancellationToken);
        if (stored is null || !Guid.TryParse(stored, out var userId))
            throw Fail.Validation(nameof(request.ResetToken), PasswordReset.InvalidTokenMessage);

        // Single use: consume before acting (the policy was validated up front).
        await cache.RemoveAsync(key, cancellationToken);

        await accounts.ResetPasswordAsync(userId, request.NewPassword, cancellationToken);
        await refreshTokens.RevokeAllSessionsAsync(userId, SessionRevokeReason.PasswordChange, cancellationToken);
    }
}
