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
    string? Notes = null) : IRequest<TenantSubscriptionStatusDto>;

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

internal sealed class SubscribeTenantCommandHandler : IRequestHandler<SubscribeTenantCommand, TenantSubscriptionStatusDto>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantSubscriptionRepository _subscriptionRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITenantService _tenantService;
    private readonly ICacheService _cacheService;
    private readonly IUnitOfWork _unitOfWork;

    public SubscribeTenantCommandHandler(
        ITenantRepository tenantRepository,
        ITenantSubscriptionRepository subscriptionRepository,
        ICurrentUserService currentUserService,
        ITenantService tenantService,
        ICacheService cacheService,
        IUnitOfWork unitOfWork)
    {
        _tenantRepository = tenantRepository;
        _subscriptionRepository = subscriptionRepository;
        _currentUserService = currentUserService;
        _tenantService = tenantService;
        _cacheService = cacheService;
        _unitOfWork = unitOfWork;
    }

    public async Task<TenantSubscriptionStatusDto> Handle(SubscribeTenantCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? _tenantService.TenantId;
        if (tenantId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("No active tenant context found.");
        }

        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant", tenantId);

        // Calculate amount based on Tier & Billing Cycle
        var amount = CalculatePrice(request.Tier, request.BillingCycle);
        var currency = tenant.DefaultCurrency ?? "BDT";

        // Activate or renew subscription
        tenant.Subscribe(request.Tier, request.BillingCycle);
        _tenantRepository.Update(tenant);

        // Record financial audit & invoice entry
        var record = TenantSubscriptionRecord.Create(
            tenantId: tenant.Id,
            tier: request.Tier,
            billingCycle: request.BillingCycle,
            amount: amount,
            currency: currency,
            startedAtUtc: DateTime.UtcNow,
            expiresAtUtc: tenant.SubscriptionExpiresAt,
            paymentMethod: request.PaymentMethod,
            paymentReference: request.PaymentReference,
            notes: request.Notes ?? $"Subscribed to {request.Tier} ({request.BillingCycle})"
        );

        await _subscriptionRepository.AddAsync(record, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Invalidate tenant middleware cache so changes take effect immediately
        await _cacheService.RemoveAsync($"tenant:{tenant.Id}:context", cancellationToken);
        if (!string.IsNullOrEmpty(tenant.Slug))
        {
            await _cacheService.RemoveAsync($"tenant:slug:{tenant.Slug}:context", cancellationToken);
        }

        int? daysRemaining = null;
        if (tenant.SubscriptionExpiresAt.HasValue)
        {
            daysRemaining = Math.Max(0, (int)Math.Ceiling((tenant.SubscriptionExpiresAt.Value - DateTime.UtcNow).TotalDays));
        }

        var (usersCount, farmsCount, animalsCount) = await _tenantRepository.GetTenantUsageCountsAsync(tenantId, cancellationToken);

        return new TenantSubscriptionStatusDto(
            TenantId: tenant.Id,
            TenantName: tenant.Name,
            Tier: tenant.SubscriptionTier.ToString(),
            BillingCycle: tenant.BillingCycle.ToString(),
            Status: tenant.Status.ToString(),
            IsTrial: tenant.IsTrial,
            TrialDays: tenant.TrialDays,
            TrialEndsAtUtc: tenant.TrialEndsAtUtc,
            TrialDaysRemaining: 0,
            SubscriptionExpiresAtUtc: tenant.SubscriptionExpiresAt,
            DaysRemaining: daysRemaining,
            HasUsedTrial: tenant.HasUsedTrial,
            MaxUsers: tenant.MaxUsers,
            CurrentUsers: usersCount,
            MaxFarms: tenant.MaxFarms,
            CurrentFarms: farmsCount,
            MaxAnimals: tenant.MaxAnimals,
            CurrentAnimals: animalsCount
        );
    }

    private static decimal CalculatePrice(SubscriptionTier tier, SubscriptionBillingCycle cycle) => (tier, cycle) switch
    {
        (SubscriptionTier.Starter, SubscriptionBillingCycle.Monthly) => 1500m,
        (SubscriptionTier.Starter, SubscriptionBillingCycle.Yearly) => 15000m,
        (SubscriptionTier.Starter, SubscriptionBillingCycle.OneTime) => 35000m,

        (SubscriptionTier.Standard, SubscriptionBillingCycle.Monthly) => 3500m,
        (SubscriptionTier.Standard, SubscriptionBillingCycle.Yearly) => 35000m,
        (SubscriptionTier.Standard, SubscriptionBillingCycle.OneTime) => 75000m,

        (SubscriptionTier.Professional, SubscriptionBillingCycle.Monthly) => 7500m,
        (SubscriptionTier.Professional, SubscriptionBillingCycle.Yearly) => 75000m,
        (SubscriptionTier.Professional, SubscriptionBillingCycle.OneTime) => 160000m,

        (SubscriptionTier.Enterprise, SubscriptionBillingCycle.Monthly) => 15000m,
        (SubscriptionTier.Enterprise, SubscriptionBillingCycle.Yearly) => 150000m,
        (SubscriptionTier.Enterprise, SubscriptionBillingCycle.OneTime) => 350000m,

        _ => 1500m
    };
}
