using System.Globalization;
using System.Text.Json;
using Farm360.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Farm360.Infrastructure.Payments;

/// <summary>
/// SSLCommerz hosted-checkout integration. Ships pointed at SSLCommerz's public sandbox store
/// (store_id "testbox") by default, so checkout is genuinely testable before any real merchant
/// account exists -- swap <see cref="SslCommerzOptions.StoreId"/>/<see cref="SslCommerzOptions.StorePassword"/>
/// and set IsSandbox to false to go live.
/// </summary>
public sealed class SslCommerzGatewayService : IPaymentGatewayService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SslCommerzOptions _options;
    private readonly ILogger<SslCommerzGatewayService> _logger;

    public SslCommerzGatewayService(
        IHttpClientFactory httpClientFactory,
        IOptions<SslCommerzOptions> options,
        ILogger<SslCommerzGatewayService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PaymentSessionResult> InitiateSessionAsync(PaymentSessionRequest request, CancellationToken cancellationToken = default)
    {
        using var client = _httpClientFactory.CreateClient();

        var form = new Dictionary<string, string>
        {
            ["store_id"] = _options.StoreId,
            ["store_passwd"] = _options.StorePassword,
            ["total_amount"] = request.Amount.ToString("F2", CultureInfo.InvariantCulture),
            ["currency"] = request.Currency,
            ["tran_id"] = request.TransactionId,
            ["success_url"] = $"{_options.ApiBaseUrl}/api/v1/subscriptions/checkout/success",
            ["fail_url"] = $"{_options.ApiBaseUrl}/api/v1/subscriptions/checkout/fail",
            ["cancel_url"] = $"{_options.ApiBaseUrl}/api/v1/subscriptions/checkout/cancel",
            ["ipn_url"] = $"{_options.ApiBaseUrl}/api/v1/subscriptions/checkout/ipn",
            ["shipping_method"] = "NO",
            ["product_name"] = request.ProductName,
            ["product_category"] = "Subscription",
            ["product_profile"] = "general",
            ["num_of_item"] = "1",
            ["cus_name"] = request.CustomerName,
            ["cus_email"] = request.CustomerEmail,
            ["cus_add1"] = "N/A",
            ["cus_city"] = "Dhaka",
            ["cus_postcode"] = "1200",
            ["cus_country"] = "Bangladesh",
            ["cus_phone"] = string.IsNullOrWhiteSpace(request.CustomerPhone) ? "N/A" : request.CustomerPhone,
        };

        try
        {
            using var content = new FormUrlEncodedContent(form);
            using var response = await client
                .PostAsync(new Uri($"{_options.GatewayBaseUrl}/gwprocess/v4/api.php"), content, cancellationToken)
                .ConfigureAwait(false);

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            var status = root.TryGetProperty("status", out var statusEl) ? statusEl.GetString() : null;

            if (!response.IsSuccessStatusCode || !string.Equals(status, "SUCCESS", StringComparison.OrdinalIgnoreCase))
            {
                var reason = root.TryGetProperty("failedreason", out var reasonEl)
                    ? reasonEl.GetString()
                    : $"Gateway returned status '{status}'.";
                _logger.LogWarning("SSLCommerz session initiation failed for {TransactionId}: {Reason}", request.TransactionId, reason);
                return new PaymentSessionResult(false, null, reason);
            }

            var gatewayUrl = root.TryGetProperty("GatewayPageURL", out var urlEl) ? urlEl.GetString() : null;
            return new PaymentSessionResult(true, gatewayUrl, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            _logger.LogError(ex, "SSLCommerz session initiation threw for {TransactionId}", request.TransactionId);
            return new PaymentSessionResult(false, null, "Could not reach the payment gateway. Please try again.");
        }
    }

    public async Task<PaymentValidationResult> ValidateTransactionAsync(string validationId, CancellationToken cancellationToken = default)
    {
        using var client = _httpClientFactory.CreateClient();

        var url = $"{_options.GatewayBaseUrl}/validator/api/validationserverAPI.php" +
                  $"?val_id={Uri.EscapeDataString(validationId)}" +
                  $"&store_id={Uri.EscapeDataString(_options.StoreId)}" +
                  $"&store_passwd={Uri.EscapeDataString(_options.StorePassword)}" +
                  "&format=json";

        try
        {
            using var response = await client.GetAsync(new Uri(url), cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            var status = root.TryGetProperty("status", out var statusEl) ? statusEl.GetString() : null;
            var isValid = string.Equals(status, "VALID", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "VALIDATED", StringComparison.OrdinalIgnoreCase);

            var tranId = root.TryGetProperty("tran_id", out var tranEl) ? tranEl.GetString() : null;
            var currency = root.TryGetProperty("currency", out var currEl) ? currEl.GetString() : null;

            decimal? amount = null;
            if (root.TryGetProperty("amount", out var amountEl))
            {
                var amountStr = amountEl.ValueKind == JsonValueKind.String ? amountEl.GetString() : amountEl.ToString();
                if (decimal.TryParse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                {
                    amount = parsed;
                }
            }

            if (!isValid)
            {
                _logger.LogWarning("SSLCommerz validation for val_id {ValId} returned status '{Status}'.", validationId, status);
            }

            return new PaymentValidationResult(isValid, tranId, amount, currency, status);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            _logger.LogError(ex, "SSLCommerz validation threw for val_id {ValId}", validationId);
            return new PaymentValidationResult(false, null, null, null, "ValidationRequestFailed");
        }
    }
}
