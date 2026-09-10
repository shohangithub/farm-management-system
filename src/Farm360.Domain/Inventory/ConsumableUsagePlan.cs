using Farm360.Domain.Common;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Exceptions;

namespace Farm360.Domain.Inventory;

public class ConsumableUsagePlan : AuditableEntity, IAggregateRoot
{
    private readonly List<ConsumableUsagePlanItem> _items = [];

    public Guid FarmId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public ConsumableUsagePlanStatus Status { get; private set; } = ConsumableUsagePlanStatus.Active;
    public DateOnly StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }

    public IReadOnlyCollection<ConsumableUsagePlanItem> Items => _items.AsReadOnly();

    private ConsumableUsagePlan() { }

    public ConsumableUsagePlan(
        Guid id,
        Guid tenantId,
        Guid farmId,
        string name,
        DateOnly startDate,
        DateOnly? endDate = null,
        string? description = null)
        : base(id, tenantId)
    {
        if (farmId == Guid.Empty)
            throw new InventoryDomainException("FarmId cannot be empty.");
        if (string.IsNullOrWhiteSpace(name))
            throw new InventoryDomainException("Plan name cannot be empty.");
        if (endDate.HasValue && endDate.Value < startDate)
            throw new InventoryDomainException("End date cannot be earlier than start date.");

        FarmId = farmId;
        Name = name.Trim();
        StartDate = startDate;
        EndDate = endDate;
        Description = description?.Trim();
        Status = ConsumableUsagePlanStatus.Active;
    }

    public void UpdateDetails(
        string name,
        DateOnly startDate,
        DateOnly? endDate = null,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InventoryDomainException("Plan name cannot be empty.");
        if (endDate.HasValue && endDate.Value < startDate)
            throw new InventoryDomainException("End date cannot be earlier than start date.");

        Name = name.Trim();
        StartDate = startDate;
        EndDate = endDate;
        Description = description?.Trim();
    }

    public void AddItem(Guid inventoryItemId, decimal plannedQuantityPerDay, string? notes = null)
    {
        if (inventoryItemId == Guid.Empty)
            throw new InventoryDomainException("Inventory item ID cannot be empty.");
        if (plannedQuantityPerDay <= 0)
            throw new InventoryDomainException("Planned quantity per day must be greater than zero.");

        var existing = _items.FirstOrDefault(i => i.InventoryItemId == inventoryItemId);
        if (existing != null)
        {
            existing.UpdateQuantity(plannedQuantityPerDay, notes);
            return;
        }

        var item = new ConsumableUsagePlanItem(Guid.NewGuid(), Id, inventoryItemId, plannedQuantityPerDay, notes);
        _items.Add(item);
    }

    public void RemoveItem(Guid planItemId)
    {
        var item = _items.FirstOrDefault(i => i.Id == planItemId);
        if (item != null)
        {
            _items.Remove(item);
        }
    }

    public void ClearItems()
    {
        _items.Clear();
    }

    public void Activate()
    {
        Status = ConsumableUsagePlanStatus.Active;
    }

    public void Pause()
    {
        Status = ConsumableUsagePlanStatus.Paused;
    }

    public void Complete()
    {
        Status = ConsumableUsagePlanStatus.Completed;
        EndDate = DateOnly.FromDateTime(DateTime.UtcNow);
    }
}

public class ConsumableUsagePlanItem : BaseEntity
{
    public Guid ConsumableUsagePlanId { get; private set; }
    public Guid InventoryItemId { get; private set; }
    public decimal PlannedQuantityPerDay { get; private set; }
    public string? Notes { get; private set; }

    private ConsumableUsagePlanItem() { }

    internal ConsumableUsagePlanItem(
        Guid id,
        Guid consumableUsagePlanId,
        Guid inventoryItemId,
        decimal plannedQuantityPerDay,
        string? notes = null)
        : base(id)
    {
        ConsumableUsagePlanId = consumableUsagePlanId;
        InventoryItemId = inventoryItemId;
        PlannedQuantityPerDay = plannedQuantityPerDay;
        Notes = notes?.Trim();
    }

    public void UpdateQuantity(decimal plannedQuantityPerDay, string? notes = null)
    {
        if (plannedQuantityPerDay <= 0)
            throw new InventoryDomainException("Planned quantity per day must be greater than zero.");

        PlannedQuantityPerDay = plannedQuantityPerDay;
        if (notes != null)
        {
            Notes = notes.Trim();
        }
    }
}
