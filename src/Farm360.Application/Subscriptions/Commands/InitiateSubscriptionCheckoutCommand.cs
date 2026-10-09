using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Tenancy;
using Farm360.Domain.Tenancy.Repositories;
using FluentValidation;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Application.Subscriptions.Commands;

/// <summary>
/// Starts a verified online checkout (SSLCommerz) for a subscription, as opposed to
/// <see cref="SubscribeTenantCommand"/>'s self-reported reference. The subscription activates
/// automatically once the gateway confirms payment -- see
/// <see cref="FinalizeSubscriptionCheckoutCommand"/>, triggered by its IPN callback.
/// </summary>
public sealed record InitiateSubscriptionCheckoutCommand(
    SubscriptionTier Tier,
    SubscriptionBillingCycle BillingCycle) : IRequest<CheckoutSessionDto>;

public sealed record CheckoutSessionDto(bool Success, string? GatewayPageUrl, string? FailedReason);

public sealed class InitiateSubscriptionCheckoutCommandValidator : AbstractValidator<InitiateSubscriptionCheckoutCommand>
{
    public InitiateSubscriptionCheckoutCommandValidator()
    {
        RuleFor(x => x.Tier).IsInEnum();
        RuleFor(x => x.BillingCycle)
            .Must(c => c == SubscriptionBillingCycle.Monthly || c == SubscriptionBillingCycle.Yearly || c == SubscriptionBillingCycle.OneTime)
            .WithMessage("Billing cycle must be Monthly, Yearly, or OneTime.");
    }
}

internal sealed class InitiateSubscriptionCheckoutCommandHandler
    : IRequestHandler<InitiateSubscriptionCheckoutCommand, CheckoutSessionDto>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantSubscriptionRepository _subscriptionRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITenantService _tenantService;
    private readonly IPaymentGatewayService _gateway;
    private readonly IUnitOfWork _unitOfWork;

    public InitiateSubscriptionCheckoutCommandHandler(
        ITenantRepository tenantRepository,
        ITenantSubscriptionRepository subscriptionRepository,
        ICurrentUserService currentUserService,
        ITenantService tenantService,
        IPaymentGatewayService gateway,
        IUnitOfWork unitOfWork)
    {
        _tenantRepository = tenantRepository;
        _subscriptionRepository = subscriptionRepository;
        _currentUserService = currentUserService;
        _tenantService = tenantService;
        _gateway = gateway;
        _unitOfWork = unitOfWork;
    }

    public async Task<CheckoutSessionDto> Handle(InitiateSubscriptionCheckoutCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? _tenantService.TenantId;
        if (tenantId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("No active tenant context found.");
        }

        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant", tenantId);

        var amount = SubscriptionPlanCatalog.PriceFor(request.Tier, request.BillingCycle);
        var currency = tenant.DefaultCurrency ?? "BDT";
        var tranId = $"F360-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

        var projectedExpiry = request.BillingCycle switch
        {
            SubscriptionBillingCycle.OneTime => (DateTime?)null,
            SubscriptionBillingCycle.Yearly => DateTime.UtcNow.AddYears(1),
            _ => DateTime.UtcNow.AddMonths(1),
        };

        // Recorded Pending immediately, keyed by tran_id, so the IPN/success callback (which only
        // carries that id) can find exactly which request to finalize.
        var record = TenantSubscriptionRecord.Create(
            tenantId: tenant.Id,
            tier: request.Tier,
            billingCycle: request.BillingCycle,
            amount: amount,
            currency: currency,
            startedAtUtc: DateTime.UtcNow,
            expiresAtUtc: projectedExpiry,
            paymentMethod: "SSLCommerz",
            paymentReference: tranId,
            notes: $"Online checkout: {request.Tier} ({request.BillingCycle})",
            status: "Pending");

        await _subscriptionRepository.AddAsync(record, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var session = await _gateway.InitiateSessionAsync(new PaymentSessionRequest(
            TransactionId: tranId,
            Amount: amount,
            Currency: currency,
            CustomerName: tenant.Name,
            CustomerEmail: $"{tenant.Slug}@farm360.invoice",
            CustomerPhone: null,
            ProductName: $"Farm360 {request.Tier} ({request.BillingCycle})"
        ), cancellationToken);

        if (!session.Success)
        {
            record.MarkRejected(session.FailedReason ?? "Gateway session could not be created.");
            _subscriptionRepository.Update(record);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new CheckoutSessionDto(session.Success, session.GatewayPageUrl, session.FailedReason);
    }
}
