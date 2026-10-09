using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Subscriptions.DTOs;
using Farm360.Domain.Tenancy;
using Farm360.Domain.Tenancy.Repositories;
using FluentValidation;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Application.Subscriptions.Commands;

public sealed record SubscribeTenantCommand(
    SubscriptionTier Tier,
    SubscriptionBillingCycle BillingCycle,
    string PaymentMethod,
    string? PaymentReference = null,
    string? Notes = null) : IRequest<TenantSubscriptionRecordDto>;

public sealed class SubscribeTenantCommandValidator : AbstractValidator<SubscribeTenantCommand>
{
    public SubscribeTenantCommandValidator()
    {
        RuleFor(x => x.Tier)
            .IsInEnum()
            .WithMessage("A valid subscription tier must be selected.");

        RuleFor(x => x.BillingCycle)
            .Must(c => c == SubscriptionBillingCycle.Monthly || c == SubscriptionBillingCycle.Yearly || c == SubscriptionBillingCycle.OneTime)
            .WithMessage("Billing cycle must be Monthly, Yearly, or OneTime.");

        RuleFor(x => x.PaymentMethod)
            .NotEmpty()
            .WithMessage("Payment method is required.");
    }
}

internal sealed class SubscribeTenantCommandHandler : IRequestHandler<SubscribeTenantCommand, TenantSubscriptionRecordDto>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantSubscriptionRepository _subscriptionRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITenantService _tenantService;
    private readonly IUnitOfWork _unitOfWork;

    public SubscribeTenantCommandHandler(
        ITenantRepository tenantRepository,
        ITenantSubscriptionRepository subscriptionRepository,
        ICurrentUserService currentUserService,
        ITenantService tenantService,
        IUnitOfWork unitOfWork)
    {
        _tenantRepository = tenantRepository;
        _subscriptionRepository = subscriptionRepository;
        _currentUserService = currentUserService;
        _tenantService = tenantService;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Records a tenant's self-reported payment (bKash/Nagad/card/bank reference typed by the
    /// user) as <c>Pending</c>. It does NOT activate the subscription -- there is no gateway here
    /// to verify the claim against, so an admin must confirm it actually arrived before the tenant
    /// gets the access they're reporting having paid for. A verified gateway (e.g. SSLCommerz
    /// checkout) activates immediately via its own callback, bypassing this manual review.
    /// </summary>
    public async Task<TenantSubscriptionRecordDto> Handle(SubscribeTenantCommand request, CancellationToken cancellationToken)
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

        var projectedExpiry = request.BillingCycle switch
        {
            SubscriptionBillingCycle.OneTime => (DateTime?)null,
            SubscriptionBillingCycle.Yearly => DateTime.UtcNow.AddYears(1),
            _ => DateTime.UtcNow.AddMonths(1),
        };

        var record = TenantSubscriptionRecord.Create(
            tenantId: tenant.Id,
            tier: request.Tier,
            billingCycle: request.BillingCycle,
            amount: amount,
            currency: currency,
            startedAtUtc: DateTime.UtcNow,
            expiresAtUtc: projectedExpiry,
            paymentMethod: request.PaymentMethod,
            paymentReference: request.PaymentReference,
            notes: request.Notes ?? $"Subscription request: {request.Tier} ({request.BillingCycle}) -- pending verification",
            status: "Pending"
        );

        await _subscriptionRepository.AddAsync(record, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new TenantSubscriptionRecordDto(
            Id: record.Id,
            Tier: record.Tier.ToString(),
            BillingCycle: record.BillingCycle.ToString(),
            Amount: record.Amount,
            Currency: record.Currency,
            StartedAtUtc: record.StartedAtUtc,
            ExpiresAtUtc: record.ExpiresAtUtc,
            PaymentMethod: record.PaymentMethod,
            PaymentReference: record.PaymentReference,
            Status: record.Status,
            InvoiceNumber: record.InvoiceNumber,
            Notes: record.Notes,
            CreatedAtUtc: record.CreatedAtUtc);
    }
}
