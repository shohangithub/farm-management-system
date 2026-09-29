using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Subscriptions.DTOs;
using Farm360.Domain.Tenancy.Repositories;
using FluentValidation;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Application.Subscriptions.Commands;

public sealed record AdminExtendTrialCommand(Guid TenantId, int AdditionalDays) : IRequest<TenantSubscriptionStatusDto>;

public sealed class AdminExtendTrialCommandValidator : AbstractValidator<AdminExtendTrialCommand>
{
    public AdminExtendTrialCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.AdditionalDays)
            .InclusiveBetween(1, 90)
            .WithMessage("Additional trial days must be between 1 and 90.");
    }
}

internal sealed class AdminExtendTrialCommandHandler : IRequestHandler<AdminExtendTrialCommand, TenantSubscriptionStatusDto>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ICacheService _cacheService;
    private readonly IUnitOfWork _unitOfWork;

    public AdminExtendTrialCommandHandler(
        ITenantRepository tenantRepository,
        ICacheService cacheService,
        IUnitOfWork unitOfWork)
    {
        _tenantRepository = tenantRepository;
        _cacheService = cacheService;
        _unitOfWork = unitOfWork;
    }

    public async Task<TenantSubscriptionStatusDto> Handle(AdminExtendTrialCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepository.GetByIdAsync(request.TenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant", request.TenantId);

        tenant.ExtendTrial(request.AdditionalDays);
        _tenantRepository.Update(tenant);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _cacheService.RemoveAsync($"tenant:{tenant.Id}:context", cancellationToken);
        if (!string.IsNullOrEmpty(tenant.Slug))
        {
            await _cacheService.RemoveAsync($"tenant:slug:{tenant.Slug}:context", cancellationToken);
        }

        int trialDaysRemaining = 0;
        if (tenant.TrialEndsAtUtc.HasValue)
        {
            trialDaysRemaining = Math.Max(0, (int)Math.Ceiling((tenant.TrialEndsAtUtc.Value - DateTime.UtcNow).TotalDays));
        }

        var (usersCount, farmsCount, animalsCount) = await _tenantRepository.GetTenantUsageCountsAsync(tenant.Id, cancellationToken);

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
            DaysRemaining: trialDaysRemaining,
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
