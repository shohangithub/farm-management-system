using System.Net;
using System.Net.Mail;
using System.Text.Json;
using Farm360.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Farm360.Infrastructure.Messaging;

/// <summary>Sends email through any SMTP relay configured under <c>Email:Smtp</c>.</summary>
public sealed class SmtpEmailService(IOptions<EmailOptions> options, ILogger<SmtpEmailService> logger) : IEmailService
{
    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var smtp = options.Value.Smtp;
        if (string.IsNullOrWhiteSpace(smtp.Host) || string.IsNullOrWhiteSpace(smtp.FromAddress))
            throw new InvalidOperationException("Email is not configured (Email:Smtp:Host / FromAddress).");

        using var message = new MailMessage
        {
            From = new MailAddress(smtp.FromAddress, smtp.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true,
        };
        message.To.Add(new MailAddress(to));

        using var client = new SmtpClient(smtp.Host, smtp.Port) { EnableSsl = smtp.EnableSsl };
        if (!string.IsNullOrWhiteSpace(smtp.Username))
            client.Credentials = new NetworkCredential(smtp.Username, smtp.Password);

        try
        {
            await client.SendMailAsync(message, cancellationToken);
        }
        catch (SmtpException ex)
        {
            logger.LogError(ex, "Email to {To} failed: {Subject}", to, subject);
            throw new InvalidOperationException("The email could not be sent.", ex);
        }
    }

    /// <summary>No template engine yet: renders the data as a simple key/value table.</summary>
    public Task SendTemplatedAsync(string to, string templateId, object templateData, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.SerializeToElement(templateData);
        var rows = json.ValueKind == JsonValueKind.Object
            ? string.Concat(json.EnumerateObject().Select(p =>
                $"<tr><td style=\"padding:4px 12px 4px 0;color:#555\">{WebUtility.HtmlEncode(p.Name)}</td><td>{WebUtility.HtmlEncode(p.Value.ToString())}</td></tr>"))
            : WebUtility.HtmlEncode(json.ToString());
        return SendAsync(to, $"Farm360 - {templateId}", $"<table>{rows}</table>", cancellationToken);
    }
}
