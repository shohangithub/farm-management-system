namespace Farm360.Application.Common.Interfaces;

/// <summary>
/// Abstraction over a hosted-checkout payment gateway (SSLCommerz today; a second implementation
/// -- e.g. bKash's direct Merchant API -- can be added later behind the same contract).
/// </summary>
public interface IPaymentGatewayService
{
    /// <summary>Starts a hosted checkout session and returns the URL to redirect the payer to.</summary>
    Task<PaymentSessionResult> InitiateSessionAsync(PaymentSessionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-verifies a transaction server-to-server against the gateway's own records. Never trust
    /// a success redirect or an IPN payload's claimed amount/status alone -- always confirm here.
    /// </summary>
    Task<PaymentValidationResult> ValidateTransactionAsync(string validationId, CancellationToken cancellationToken = default);
}

public sealed record PaymentSessionRequest(
    string TransactionId,
    decimal Amount,
    string Currency,
    string CustomerName,
    string CustomerEmail,
    string? CustomerPhone,
    string ProductName);

public sealed record PaymentSessionResult(bool Success, string? GatewayPageUrl, string? FailedReason);

public sealed record PaymentValidationResult(
    bool IsValid,
    string? TransactionId,
    decimal? Amount,
    string? Currency,
    string? RawStatus);
