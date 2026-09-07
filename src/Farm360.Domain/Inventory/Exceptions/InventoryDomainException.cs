using Farm360.Domain.Exceptions;

namespace Farm360.Domain.Inventory.Exceptions;

public class InventoryDomainException : DomainException
{
    public InventoryDomainException(string message) : base(message)
    {
    }

    public InventoryDomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
