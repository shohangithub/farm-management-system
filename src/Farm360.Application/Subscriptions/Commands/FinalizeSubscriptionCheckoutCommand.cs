using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Tenancy.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Application.Subscriptions.Commands;

/// <summary>
/// Confirms an online checkout transaction and activates the subscription it describes. Called
/// from both SSLCommerz's IPN (server-to-server, authoritative) and the browser's success-url
/// redirect (best-effort, since the browser can be closed before it fires) -- idempotent either
/// way, since the second call simply finds the record already <c>Completed</c> and does nothing.
/// </summary>
/// <remarks>
/// The <paramref name="TranId"/>/<paramref name="ValId"/> pair is never trusted at face value:
/// <see cref="IPaymentGatewayService.ValidateTransactionAsync"/> re-queries the gateway itself for
/// what actually happened, which is the only thing that can't be forged by someone POSTing a fake
/// callback.
/// </remarks>
public sealed record FinalizeSubscriptionCheckoutCommand(string TranId, string ValId) : IRequest<bool>;

internal sealed class FinalizeSubscriptionCheckoutCommandHandler : IRequestHandler<FinalizeSubscriptionCheckoutCommand, bool>
{
    private readonly ITenantSubscriptionRepository _subscriptionRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IPaymentGatewayService _gateway;
    private readonly ICacheService _cacheService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<FinalizeSubscriptionCheckoutCommandHandler> _logger;

    public FinalizeSubscriptionCheckoutCommandHandler(
        ITenantSubscriptionRepository subscriptionRepository,
        ITenantRepository tenantRepository,
        IPaymentGatewayService gateway,
        ICacheService cacheService,
        IUnitOfWork unitOfWork,
        ILogger<FinalizeSubscriptionCheckoutCommandHandler> logger)
    {
        _subscriptionRepository = subscriptionRepository;
        _tenantRepository = tenantRepository;
        _gateway = gateway;
        _cacheService = cacheService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<bool> Handle(FinalizeSubscriptionCheckoutCommand request, CancellationToken cancellationToken)
    {
        var record = await _subscriptionRepository.GetByPaymentReferenceAsync(request.TranId, cancellationToken);
        if (record is null)
        {
            _logger.LogWarning("Checkout finalize: no subscription record for tran_id {TranId}.", request.TranId);
            return false;
        }

        if (record.Status != "Pending")
        {
            // Already finalized by the other caller (IPN vs. success-url race). Not an error.
            return record.Status == "Completed";
        }

        var validation = await _gateway.ValidateTransactionAsync(request.ValId, cancellationToken);

        var amountMatches = validation.Amount.HasValue && Math.Abs(validation.Amount.Value - record.Amount) < 1m;
        var tranIdMatches = string.Equals(validation.TransactionId, request.TranId, StringComparison.OrdinalIgnoreCase);

        if (!validation.IsValid || !tranIdMatches || !amountMatches)
        {
            _logger.LogWarning(
                "Checkout finalize: gateway validation failed for tran_id {TranId} (valid={IsValid}, tranMatch={TranMatch}, amountMatch={AmountMatch}, status={Status}).",
                request.TranId, validation.IsValid, tranIdMatches, amountMatches, validation.RawStatus);

            record.MarkRejected($"Gateway validation failed (status: {validation.RawStatus}).");
            _subscriptionRepository.Update(record);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return false;
        }

        var tenant = await _tenantRepository.GetByIdAsync(record.TenantId, cancellationToken);
        if (tenant is null)
        {
            _logger.LogError("Checkout finalize: tenant {TenantId} not found for record {RecordId}.", record.TenantId, record.Id);
            return false;
        }

        tenant.Subscribe(record.Tier, record.BillingCycle, record.ExpiresAtUtc);
        _tenantRepository.Update(tenant);

        record.MarkCompleted(request.ValId);
        _subscriptionRepository.Update(record);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _cacheService.RemoveAsync($"tenant:{tenant.Id}:context", cancellationToken);
        if (!string.IsNullOrEmpty(tenant.Slug))
        {
            await _cacheService.RemoveAsync($"tenant:slug:{tenant.Slug}:context", cancellationToken);
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Checkout finalized: tenant {TenantId} activated {Tier}/{BillingCycle} via tran_id {TranId}.",
                tenant.Id, record.Tier, record.BillingCycle, request.TranId);
        }

        return true;
    }
}
