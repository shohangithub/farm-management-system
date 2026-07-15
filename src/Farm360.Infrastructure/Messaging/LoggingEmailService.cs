using Farm360.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Farm360.Infrastructure.Messaging;

/// <summary>
/// Console/log-only Email service for Development and Staging environments.
/// IEmailService is defined in Application interfaces but no concrete implementation
/// existed prior to this fix.
///
/// PRODUCTION: Replace with a real mail provider (SendGrid, AWS SES, SMTP).
/// Wire in via environment-conditional registration:
///   if (env.IsProduction()) services.AddScoped&lt;IEmailService, SendGridEmailService&gt;();
///   else                   services.AddScoped&lt;IEmailService, LoggingEmailService&gt;();
///
/// References:
///   docs/7_Farm360_Solution_Structure.md §Messaging: EmailService.cs
///   docs/9_Farm360_Auth_Architecture.md §3: Email verification flow
/// </summary>
public sealed class LoggingEmailService(ILogger<LoggingEmailService> logger) : IEmailService
{
    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("[DEV-EMAIL] Would send email to {To} | Subject: {Subject}", to, subject);
        return Task.CompletedTask;
    }

    public Task SendTemplatedAsync(string to, string templateId, object templateData, CancellationToken cancellationToken = default)
    {
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("[DEV-EMAIL] Would send templated email to {To} | TemplateId: {TemplateId}", to, templateId);
        return Task.CompletedTask;
    }
}
