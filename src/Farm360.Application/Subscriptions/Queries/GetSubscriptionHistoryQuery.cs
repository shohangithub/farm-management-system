using Farm360.Application.Common.Interfaces;
using Farm360.Application.Subscriptions.DTOs;
using Farm360.Domain.Tenancy.Repositories;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Application.Subscriptions.Queries;

public sealed record GetSubscriptionHistoryQuery : IRequest<IReadOnlyList<TenantSubscriptionRecordDto>>;

internal sealed class GetSubscriptionHistoryQueryHandler : IRequestHandler<GetSubscriptionHistoryQuery, IReadOnlyList<TenantSubscriptionRecordDto>>
{
    private readonly ITenantSubscriptionRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITenantService _tenantService;

    public GetSubscriptionHistoryQueryHandler(
        ITenantSubscriptionRepository repository,
        ICurrentUserService currentUserService,
        ITenantService tenantService)
    {
        _repository = repository;
        _currentUserService = currentUserService;
        _tenantService = tenantService;
    }

    public async Task<IReadOnlyList<TenantSubscriptionRecordDto>> Handle(GetSubscriptionHistoryQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId ?? _tenantService.TenantId;
        if (tenantId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("No tenant context found for the current user.");
        }

        var records = await _repository.GetByTenantIdAsync(tenantId, cancellationToken);

        return records.Select(r => new TenantSubscriptionRecordDto(
            Id: r.Id,
            Tier: r.Tier.ToString(),
            BillingCycle: r.BillingCycle.ToString(),
            Amount: r.Amount,
            Currency: r.Currency,
            StartedAtUtc: r.StartedAtUtc,
            ExpiresAtUtc: r.ExpiresAtUtc,
            PaymentMethod: r.PaymentMethod,
            PaymentReference: r.PaymentReference,
            Status: r.Status,
            InvoiceNumber: r.InvoiceNumber,
            Notes: r.Notes,
            CreatedAtUtc: r.CreatedAtUtc
        )).ToList();
    }
}
