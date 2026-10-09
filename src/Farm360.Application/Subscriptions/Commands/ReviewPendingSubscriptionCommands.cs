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

/// <summary>
/// Confirms a tenant's self-reported (manual) payment reference and activates the subscription it
/// describes. The counterpart to <see cref="SubscribeTenantCommand"/> leaving a record Pending
/// instead of trusting the typed reference outright.
/// </summary>
public sealed record ApprovePendingSubscriptionCommand(
    Guid SubscriptionRecordId,
    string? VerifiedReference = null) : IRequest<TenantSubscriptionStatusDto>;

public sealed class ApprovePendingSubscriptionCommandValidator : AbstractValidator<ApprovePendingSubscriptionCommand>
{
    public ApprovePendingSubscriptionCommandValidator()
    {
        RuleFor(x => x.SubscriptionRecordId).NotEmpty();
    }
}

internal sealed class ApprovePendingSubscriptionCommandHandler
    : IRequestHandler<ApprovePendingSubscriptionCommand, TenantSubscriptionStatusDto>
{
    private readonly ITenantSubscriptionRepository _subscriptionRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly ICacheService _cacheService;
    private readonly IUnitOfWork _unitOfWork;

    public ApprovePendingSubscriptionCommandHandler(
        ITenantSubscriptionRepository subscriptionRepository,
        ITenantRepository tenantRepository,
        ICacheService cacheService,
        IUnitOfWork unitOfWork)
    {
        _subscriptionRepository = subscriptionRepository;
        _tenantRepository = tenantRepository;
        _cacheService = cacheService;
        _unitOfWork = unitOfWork;
    }

    public async Task<TenantSubscriptionStatusDto> Handle(ApprovePendingSubscriptionCommand request, CancellationToken cancellationToken)
    {
        var record = await _subscriptionRepository.GetByIdAsync(request.SubscriptionRecordId, cancellationToken)
            ?? throw new NotFoundException("TenantSubscriptionRecord", request.SubscriptionRecordId);

        var tenant = await _tenantRepository.GetByIdAsync(record.TenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant", record.TenantId);

        tenant.Subscribe(record.Tier, record.BillingCycle, record.ExpiresAtUtc);
        _tenantRepository.Update(tenant);

        record.MarkCompleted(request.VerifiedReference);
        _subscriptionRepository.Update(record);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

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
}

/// <summary>Rejects a tenant's self-reported payment -- the claimed reference did not check out.</summary>
public sealed record RejectPendingSubscriptionCommand(
    Guid SubscriptionRecordId,
    string? Reason = null) : IRequest<TenantSubscriptionRecordDto>;

public sealed class RejectPendingSubscriptionCommandValidator : AbstractValidator<RejectPendingSubscriptionCommand>
{
    public RejectPendingSubscriptionCommandValidator()
    {
        RuleFor(x => x.SubscriptionRecordId).NotEmpty();
    }
}

internal sealed class RejectPendingSubscriptionCommandHandler
    : IRequestHandler<RejectPendingSubscriptionCommand, TenantSubscriptionRecordDto>
{
    private readonly ITenantSubscriptionRepository _subscriptionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RejectPendingSubscriptionCommandHandler(
        ITenantSubscriptionRepository subscriptionRepository,
        IUnitOfWork unitOfWork)
    {
        _subscriptionRepository = subscriptionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<TenantSubscriptionRecordDto> Handle(RejectPendingSubscriptionCommand request, CancellationToken cancellationToken)
    {
        var record = await _subscriptionRepository.GetByIdAsync(request.SubscriptionRecordId, cancellationToken)
            ?? throw new NotFoundException("TenantSubscriptionRecord", request.SubscriptionRecordId);

        record.MarkRejected(request.Reason);
        _subscriptionRepository.Update(record);
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
