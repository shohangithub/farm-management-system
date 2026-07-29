namespace Farm360.Domain.Inventory.Exceptions;

public class InventoryDomainException : Exception
{
    public InventoryDomainException(string message) : base(message)
    {
    }

    public InventoryDomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
