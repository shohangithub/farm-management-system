using Farm360.Application.Subscriptions.DTOs;
using Farm360.Domain.Tenancy.Repositories;
using MediatR;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Application.Subscriptions.Queries;

/// <summary>Self-reported payments awaiting admin verification, across every tenant.</summary>
public sealed record PendingSubscriptionDto(
    System.Guid Id,
    System.Guid TenantId,
    string TenantName,
    string Tier,
    string BillingCycle,
    decimal Amount,
    string Currency,
    string PaymentMethod,
    string? PaymentReference,
    string InvoiceNumber,
    string? Notes,
    System.DateTime CreatedAtUtc);

public sealed record GetPendingSubscriptionsQuery : IRequest<IReadOnlyList<PendingSubscriptionDto>>;

internal sealed class GetPendingSubscriptionsQueryHandler
    : IRequestHandler<GetPendingSubscriptionsQuery, IReadOnlyList<PendingSubscriptionDto>>
{
    private readonly Farm360.Application.Common.Interfaces.ICurrentUserService _currentUser;
    private readonly ITenantSubscriptionRepository _subscriptionRepository;
    private readonly ITenantRepository _tenantRepository;

    public GetPendingSubscriptionsQueryHandler(
        ITenantSubscriptionRepository subscriptionRepository,
        ITenantRepository tenantRepository,
        Farm360.Application.Common.Interfaces.ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
        _subscriptionRepository = subscriptionRepository;
        _tenantRepository = tenantRepository;
    }

    public async Task<IReadOnlyList<PendingSubscriptionDto>> Handle(GetPendingSubscriptionsQuery request, CancellationToken cancellationToken)
    {
        // Defence in depth: the route already requires the PlatformAdmin policy.
        if (!_currentUser.IsSystemUser)
        {
            throw new Farm360.Application.Common.Exceptions.ForbiddenAccessException("Platform administrator access is required.");
        }

        var pending = await _subscriptionRepository.GetPendingAsync(cancellationToken);
        if (pending.Count == 0)
        {
            return [];
        }

        var tenants = await _tenantRepository.GetAllAsync(cancellationToken);
        var tenantNamesById = tenants.ToDictionary(t => t.Id, t => t.Name);

        return pending
            .Select(r => new PendingSubscriptionDto(
                Id: r.Id,
                TenantId: r.TenantId,
                TenantName: tenantNamesById.TryGetValue(r.TenantId, out var name) ? name : "(unknown tenant)",
                Tier: r.Tier.ToString(),
                BillingCycle: r.BillingCycle.ToString(),
                Amount: r.Amount,
                Currency: r.Currency,
                PaymentMethod: r.PaymentMethod,
                PaymentReference: r.PaymentReference,
                InvoiceNumber: r.InvoiceNumber,
                Notes: r.Notes,
                CreatedAtUtc: r.CreatedAtUtc))
            .ToList();
    }
}
