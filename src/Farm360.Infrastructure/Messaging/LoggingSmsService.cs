using Farm360.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Farm360.Infrastructure.Messaging;

/// <summary>
/// Console/log-only SMS service for Development and Staging environments.
/// F360-AUTH-2026-001 §4: OTP values are NEVER logged even in dev mode.
/// Only the masked phone and purpose are recorded.
///
/// PRODUCTION: Replace with an actual SMS gateway implementation
/// (e.g., Twilio, Bangladesh-specific gateway like SSL Commerz SMS, Shajgoj SMS).
/// Wire in via environment-conditional registration:
///   if (env.IsProduction()) services.AddScoped&lt;ISmsService, TwilioSmsService&gt;();
///   else                   services.AddScoped&lt;ISmsService, LoggingSmsService&gt;();
///
/// References:
///   docs/7_Farm360_Solution_Structure.md §Messaging: SmsService.cs
///   docs/9_Farm360_Auth_Architecture.md §4: OTP delivery via ISmsService
/// </summary>
public sealed class LoggingSmsService(ILogger<LoggingSmsService> logger) : ISmsService
{
    public Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
    {
        var masked = MaskPhone(phoneNumber);
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("[DEV-SMS] Would send SMS to {PhoneMasked}: {Message}", masked, message);
        return Task.CompletedTask;
    }

    public Task SendOtpAsync(string phoneNumber, string otpCode, string purpose, CancellationToken cancellationToken = default)
    {
        // OTP value is intentionally NOT logged — Constitution §11, F360-AUTH-2026-001 §4
        var masked = MaskPhone(phoneNumber);
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("[DEV-SMS] Would send OTP to {PhoneMasked} for Purpose={Purpose}", masked, purpose);
        return Task.CompletedTask;
    }

    private static string MaskPhone(string phone)
    {
        if (phone.Length < 5) return "***";
        return phone[..3] + "***" + phone[^2..];
    }
}
