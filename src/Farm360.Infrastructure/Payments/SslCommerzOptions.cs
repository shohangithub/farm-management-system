namespace Farm360.Infrastructure.Payments;

public sealed class SslCommerzOptions
{
    public const string SectionName = "SslCommerz";

    public string StoreId { get; set; } = string.Empty;
    public string StorePassword { get; set; } = string.Empty;
    public bool IsSandbox { get; set; } = true;
    public string FrontendBaseUrl { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = string.Empty;

    public string GatewayBaseUrl => IsSandbox
        ? "https://sandbox.sslcommerz.com"
        : "https://securepay.sslcommerz.com";
}
