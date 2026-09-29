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

public sealed record StartTenantTrialCommand(int TrialDays) : IRequest<TenantSubscriptionStatusDto>;

public sealed class StartTenantTrialCommandValidator : AbstractValidator<StartTenantTrialCommand>
{
    public StartTenantTrialCommandValidator()
    {
        RuleFor(x => x.TrialDays)
            .Must(d => d == 3 || d == 7 || d == 10)
            .WithMessage("Free trial duration must be either 3, 7, or 10 days.");
    }
}

internal sealed class StartTenantTrialCommandHandler : IRequestHandler<StartTenantTrialCommand, TenantSubscriptionStatusDto>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantSubscriptionRepository _subscriptionRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITenantService _tenantService;
    private readonly ICacheService _cacheService;
    private readonly IUnitOfWork _unitOfWork;

    public StartTenantTrialCommandHandler(
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

    public async Task<TenantSubscriptionStatusDto> Handle(StartTenantTrialCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? _tenantService.TenantId;
        if (tenantId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("No active tenant context found.");
        }

        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant", tenantId);

        // Start free trial
        tenant.StartTrial(request.TrialDays);
        _tenantRepository.Update(tenant);

        // Record trial entry in financial audit
        var subscriptionRecord = TenantSubscriptionRecord.Create(
            tenantId: tenant.Id,
            tier: tenant.SubscriptionTier,
            billingCycle: SubscriptionBillingCycle.Trial,
            amount: 0m,
            currency: tenant.DefaultCurrency ?? "BDT",
            startedAtUtc: DateTime.UtcNow,
            expiresAtUtc: tenant.TrialEndsAtUtc,
            paymentMethod: "Free Trial",
            paymentReference: $"TRIAL-{request.TrialDays}D",
            notes: $"Activated {request.TrialDays}-day free trial period."
        );

        await _subscriptionRepository.AddAsync(subscriptionRecord, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Invalidate tenant middleware cache so changes take effect immediately
        await _cacheService.RemoveAsync($"tenant:{tenant.Id}:context", cancellationToken);
        if (!string.IsNullOrEmpty(tenant.Slug))
        {
            await _cacheService.RemoveAsync($"tenant:slug:{tenant.Slug}:context", cancellationToken);
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
            TrialDaysRemaining: request.TrialDays,
            SubscriptionExpiresAtUtc: tenant.SubscriptionExpiresAt,
            DaysRemaining: request.TrialDays,
            HasUsedTrial: tenant.HasUsedTrial,
            MaxUsers: tenant.MaxUsers,
            CurrentUsers: usersCount,
            MaxFarms: tenant.MaxFarms,
            CurrentFarms: farmsCount,
            MaxAnimals: tenant.MaxAnimals,
            CurrentAnimals: animalsCount
        );
    }
}
