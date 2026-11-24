using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Exceptions;
using Xunit;

namespace Farm360.Domain.UnitTests.Inventory;

public class PurchaseOrderDomainTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _farmId = Guid.NewGuid();
    private readonly Guid _supplierId = Guid.NewGuid();

    private PurchaseOrder NewOrder(bool withItem = true)
    {
        var po = new PurchaseOrder(Guid.NewGuid(), _tenantId, _farmId, _supplierId, DateOnly.FromDateTime(DateTime.UtcNow));
        if (withItem)
            po.AddItem(Guid.NewGuid(), 10, 50m);
        return po;
    }

    [Fact]
    public void NewPurchaseOrder_ShouldStartAsDraft()
    {
        Assert.Equal(PurchaseOrderStatus.Draft, NewOrder().Status);
    }

    [Fact]
    public void SubmitForApproval_FromDraftWithItems_ShouldMoveToPendingApproval()
    {
        var po = NewOrder();

        po.SubmitForApproval();

        Assert.Equal(PurchaseOrderStatus.PendingApproval, po.Status);
    }

    [Fact]
    public void SubmitForApproval_WithoutItems_ShouldThrow()
    {
        var po = NewOrder(withItem: false);

        Assert.Throws<InventoryDomainException>(() => po.SubmitForApproval());
        Assert.Equal(PurchaseOrderStatus.Draft, po.Status);
    }

    [Fact]
    public void SubmitForApproval_WhenAlreadyPending_ShouldThrow()
    {
        var po = NewOrder();
        po.SubmitForApproval();

        Assert.Throws<InventoryDomainException>(() => po.SubmitForApproval());
    }

    [Fact]
    public void Cancel_FromDraft_ShouldCancelAndRecordReason()
    {
        var po = NewOrder();

        po.Cancel("Supplier out of stock");

        Assert.Equal(PurchaseOrderStatus.Cancelled, po.Status);
        Assert.Contains("Supplier out of stock", po.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Cancel_FromPendingApproval_ShouldCancel()
    {
        var po = NewOrder();
        po.SubmitForApproval();

        po.Cancel("Not needed");

        Assert.Equal(PurchaseOrderStatus.Cancelled, po.Status);
    }

    [Fact]
    public void Cancel_FromApproved_ShouldCancel()
    {
        var po = NewOrder();
        po.SubmitForApproval();
        po.Approve("manager");

        po.Cancel("Price changed");

        Assert.Equal(PurchaseOrderStatus.Cancelled, po.Status);
    }

    [Fact]
    public void Cancel_WhenFulfilled_ShouldThrow()
    {
        var po = NewOrder();
        po.SubmitForApproval();
        po.Approve("manager");
        po.Fulfill();

        Assert.Throws<InventoryDomainException>(() => po.Cancel("Too late"));
        Assert.Equal(PurchaseOrderStatus.Fulfilled, po.Status);
    }

    [Fact]
    public void Cancel_WhenAlreadyCancelled_ShouldThrow()
    {
        var po = NewOrder();
        po.Cancel("First");

        Assert.Throws<InventoryDomainException>(() => po.Cancel("Second"));
    }

    [Fact]
    public void Approve_WhenCancelled_ShouldThrow()
    {
        var po = NewOrder();
        po.SubmitForApproval();
        po.Cancel("Not needed");

        Assert.Throws<InventoryDomainException>(() => po.Approve("manager"));
    }
}
