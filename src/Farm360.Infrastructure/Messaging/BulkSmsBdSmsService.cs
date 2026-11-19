using System.Text.Json;
using Farm360.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Farm360.Infrastructure.Messaging;

/// <summary>
/// Sends SMS through the bulksmsbd.net HTTP API. A rejected or failed send is logged (masked phone,
/// never the text) and surfaces as <see cref="InvalidOperationException"/>, which callers such as the
/// password-reset flow treat as "not sent" without revealing whether the account exists.
/// </summary>
public sealed class BulkSmsBdSmsService(
    IHttpClientFactory httpClientFactory,
    IOptions<SmsOptions> options,
    ILogger<BulkSmsBdSmsService> logger) : ISmsService
{
    public const string HttpClientName = "sms-bulksmsbd";

    public Task SendOtpAsync(string phoneNumber, string otpCode, string purpose, CancellationToken cancellationToken = default) =>
        SendCoreAsync(phoneNumber, $"Your Farm360 code is {otpCode}. It expires in 5 minutes. Do not share it with anyone.", cancellationToken);

    public Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default) =>
        SendCoreAsync(phoneNumber, message, cancellationToken);

    private async Task SendCoreAsync(string phoneNumber, string message, CancellationToken cancellationToken)
    {
        var settings = options.Value.BulkSmsBd;
        if (string.IsNullOrWhiteSpace(settings.ApiKey) || string.IsNullOrWhiteSpace(settings.SenderId))
            throw new InvalidOperationException("SMS gateway is not configured (Sms:BulkSmsBd:ApiKey / SenderId).");

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["api_key"] = settings.ApiKey,
            ["type"] = "text",
            ["number"] = ToGatewayNumber(phoneNumber),
            ["senderid"] = settings.SenderId,
            ["message"] = message,
        });

        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.PostAsync(new Uri(settings.BaseUrl), content, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode || !IsAccepted(body))
            {
                if (logger.IsEnabled(LogLevel.Error))
                    logger.LogError("SMS to {PhoneMasked} rejected by gateway: HTTP {Status} {Body}",
                        Mask(phoneNumber), (int)response.StatusCode, Truncate(body));
                throw new InvalidOperationException("The SMS gateway did not accept the message.");
            }
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "SMS to {PhoneMasked} failed: gateway unreachable", Mask(phoneNumber));
            throw new InvalidOperationException("The SMS gateway could not be reached.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "SMS to {PhoneMasked} failed: gateway timed out", Mask(phoneNumber));
            throw new InvalidOperationException("The SMS gateway timed out.", ex);
        }
    }

    /// <summary>The gateway answers <c>{"response_code":202,...}</c> on success.</summary>
    internal static bool IsAccepted(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("response_code", out var code)) return false;
            var value = code.ValueKind == JsonValueKind.Number
                ? code.GetInt32()
                : int.TryParse(code.GetString(), out var parsed) ? parsed : 0;
            return value == 202;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>The gateway expects 8801XXXXXXXXX (no plus); a local 01XXXXXXXXX number gets the 88 prefix.</summary>
    internal static string ToGatewayNumber(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits.StartsWith("01", StringComparison.Ordinal) && digits.Length == 11 ? "88" + digits : digits;
    }

    private static string Mask(string phone) => phone.Length < 5 ? "***" : phone[..3] + "***" + phone[^2..];

    private static string Truncate(string s) => s.Length <= 200 ? s : s[..200];
}
