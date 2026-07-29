using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Events;
using Farm360.Domain.Inventory.Exceptions;
using Xunit;

namespace Farm360.Domain.UnitTests.Inventory;

public class InventoryDomainTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _farmId = Guid.NewGuid();

    [Fact]
    public void CreateInventoryItem_ShouldSetPropertiesCorrectly()
    {
        var item = new InventoryItem(
            Guid.NewGuid(), _tenantId, _farmId, "Maize Silage", "MZ-001",
            InventoryCategory.Feed, "kg", 200, 500, 45.0m, "Shed A");

        Assert.Equal("Maize Silage", item.Name);
        Assert.Equal("MZ-001", item.Sku);
        Assert.Equal(InventoryCategory.Feed, item.Category);
        Assert.Equal(500, item.CurrentStock);
        Assert.Equal(45.0m, item.WeightedAverageCostBdt);
        Assert.Equal(22500.0m, item.TotalValueBdt);
    }

    [Fact]
    public void ReceiveStock_ShouldRecalculateWeightedAverageCost()
    {
        // Initial stock: 100 kg @ 40 BDT/kg = 4000 BDT
        var item = new InventoryItem(
            Guid.NewGuid(), _tenantId, _farmId, "Vaccine A", "VAC-1",
            InventoryCategory.Vaccine, "dose", 20, 100, 40.0m);

        // Receive: 100 doses @ 60 BDT/dose = 6000 BDT
        // New Stock: 200 doses, Total Value: 10000 BDT => New WAC: 50 BDT/dose
        item.ReceiveStock(100, 60.0m, Guid.NewGuid());

        Assert.Equal(200, item.CurrentStock);
        Assert.Equal(50.0m, item.WeightedAverageCostBdt);
        Assert.Single(item.DomainEvents.OfType<StockReceivedEvent>());
    }

    [Fact]
    public void DeductStock_ExceedingCurrentStock_ShouldThrowInventoryDomainException()
    {
        var item = new InventoryItem(
            Guid.NewGuid(), _tenantId, _farmId, "De-wormer", "MED-1",
            InventoryCategory.Medicine, "bottle", 5, 10, 150.0m);

        Assert.Throws<InventoryDomainException>(() => item.DeductStock(15, Guid.NewGuid()));
    }

    [Fact]
    public void DeductStock_BelowReorderThreshold_ShouldRaiseLowStockAlertEvent()
    {
        var item = new InventoryItem(
            Guid.NewGuid(), _tenantId, _farmId, "Feed Concentrate", "FC-1",
            InventoryCategory.Feed, "kg", 100, 120, 35.0m);

        item.DeductStock(30, Guid.NewGuid()); // New stock = 90 (<= 100 threshold)

        Assert.Equal(90, item.CurrentStock);
        Assert.Contains(item.DomainEvents, e => e is LowStockAlertEvent);
    }
}
