namespace Farm360.Infrastructure.Messaging;

/// <summary>
/// <c>Sms</c> section. <c>Provider</c>: <c>Log</c> (default; nothing is sent, for development) or
/// <c>BulkSmsBd</c> (bulksmsbd.net HTTP API, a common Bangladeshi gateway).
/// </summary>
public sealed class SmsOptions
{
    public const string SectionName = "Sms";

    public string Provider { get; set; } = "Log";
    public BulkSmsBdOptions BulkSmsBd { get; set; } = new();
}

public sealed class BulkSmsBdOptions
{
    public string BaseUrl { get; set; } = "http://bulksmsbd.net/api/smsapi";
    public string ApiKey { get; set; } = string.Empty;
    public string SenderId { get; set; } = string.Empty;
}

/// <summary>
/// <c>Email</c> section. <c>Provider</c>: <c>Log</c> (default) or <c>Smtp</c>, which works with any SMTP
/// relay (Brevo, Amazon SES SMTP, Mailgun, Google Workspace, cPanel mail).
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string Provider { get; set; } = "Log";
    public SmtpOptions Smtp { get; set; } = new();
}

public sealed class SmtpOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "Farm360";
}
