using Farm360.Domain.Common;
using Farm360.Domain.Inventory.Exceptions;

namespace Farm360.Domain.Inventory;

public class Supplier : AuditableEntity, IAggregateRoot
{
    public string Name { get; private set; } = null!;
    public string? ContactPerson { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? Address { get; private set; }
    public string? Notes { get; private set; }
    public bool IsActive { get; private set; } = true;

    private Supplier() { }

    public Supplier(
        Guid id,
        Guid tenantId,
        string name,
        string? contactPerson = null,
        string? phone = null,
        string? email = null,
        string? address = null,
        string? notes = null)
        : base(id, tenantId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InventoryDomainException("Supplier name cannot be empty.");

        Name = name.Trim();
        ContactPerson = contactPerson?.Trim();
        Phone = phone?.Trim();
        Email = email?.Trim();
        Address = address?.Trim();
        Notes = notes?.Trim();
    }

    public void UpdateDetails(string name, string? contactPerson, string? phone, string? email, string? address, string? notes)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InventoryDomainException("Supplier name cannot be empty.");

        Name = name.Trim();
        ContactPerson = contactPerson?.Trim();
        Phone = phone?.Trim();
        Email = email?.Trim();
        Address = address?.Trim();
        Notes = notes?.Trim();
    }

    public void SetActiveStatus(bool isActive)
    {
        IsActive = isActive;
    }
}
