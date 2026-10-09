using Farm360.Domain.Inventory.Events;
using MediatR;

namespace Farm360.Application.Inventory.EventHandlers;

public sealed record PurchaseReturnCompletedNotification(PurchaseReturnCompletedEvent DomainEvent) : INotification;
