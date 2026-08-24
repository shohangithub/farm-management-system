using System;
using Farm360.Domain.Common;

namespace Farm360.Domain.Finance.Events;

public sealed record FinancialTransactionCreatedEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    Guid TransactionId,
    Guid TenantId,
    Guid FarmId,
    Enums.TransactionCategory Category,
    decimal AmountBdt,
    Guid? AnimalId) : IDomainEvent;
