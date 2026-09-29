using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Subscriptions.DTOs;
using Farm360.Domain.Tenancy.Repositories;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Application.Subscriptions.Queries;

public sealed record GetCurrentTenantSubscriptionQuery : IRequest<TenantSubscriptionStatusDto>;

internal sealed class GetCurrentTenantSubscriptionQueryHandler : IRequestHandler<GetCurrentTenantSubscriptionQuery, TenantSubscriptionStatusDto>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantService _tenantService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public GetCurrentTenantSubscriptionQueryHandler(
        ITenantRepository tenantRepository,
        ITenantService tenantService,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _tenantRepository = tenantRepository;
        _tenantService = tenantService;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task<TenantSubscriptionStatusDto> Handle(GetCurrentTenantSubscriptionQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? _tenantService.TenantId;
        if (tenantId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("No tenant context found for the current user.");
        }

        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant", tenantId);

        var oldStatus = tenant.Status;
        var currentStatus = tenant.CheckAndRefreshStatus();
        if (oldStatus != currentStatus)
        {
            _tenantRepository.Update(tenant);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var now = DateTime.UtcNow;

        int trialDaysRemaining = 0;
        if (tenant.IsTrial && tenant.TrialEndsAtUtc.HasValue)
        {
            trialDaysRemaining = Math.Max(0, (int)Math.Ceiling((tenant.TrialEndsAtUtc.Value - now).TotalDays));
        }

        int? daysRemaining = null;
        if (tenant.SubscriptionExpiresAt.HasValue)
        {
            daysRemaining = Math.Max(0, (int)Math.Ceiling((tenant.SubscriptionExpiresAt.Value - now).TotalDays));
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
            TrialDaysRemaining: trialDaysRemaining,
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
}
