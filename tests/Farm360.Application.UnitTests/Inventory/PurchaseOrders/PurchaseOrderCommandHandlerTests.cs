using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Inventory.Commands.PurchaseOrders;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Exceptions;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using Moq;
using Xunit;

namespace Farm360.Application.UnitTests.Inventory.PurchaseOrders;

public class PurchaseOrderCommandHandlerTests
{
    private readonly Mock<IPurchaseOrderRepository> _poRepoMock = new();
    private readonly Mock<ITenantService> _tenantServiceMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _farmId = Guid.NewGuid();
    private readonly Guid _supplierId = Guid.NewGuid();

    public PurchaseOrderCommandHandlerTests()
    {
        _tenantServiceMock.Setup(x => x.TenantId).Returns(_tenantId);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    private PurchaseOrder NewOrder()
    {
        var po = new PurchaseOrder(Guid.NewGuid(), _tenantId, _farmId, _supplierId, DateOnly.FromDateTime(DateTime.UtcNow));
        po.AddItem(Guid.NewGuid(), 10, 50m);
        return po;
    }

    private CreatePurchaseOrderCommand NewCreateCommand(bool? submit = null)
    {
        var items = new List<PurchaseOrderItemDto> { new(Guid.NewGuid(), 5, 100m) };
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return submit is null
            ? new CreatePurchaseOrderCommand(_farmId, _supplierId, today, null, null, items)
            : new CreatePurchaseOrderCommand(_farmId, _supplierId, today, null, null, items, submit.Value);
    }

    [Fact]
    public async Task Create_ByDefault_ShouldSubmitForApproval()
    {
        PurchaseOrder? saved = null;
        _poRepoMock.Setup(x => x.AddAsync(It.IsAny<PurchaseOrder>(), It.IsAny<CancellationToken>()))
            .Callback<PurchaseOrder, CancellationToken>((po, _) => saved = po)
            .Returns(Task.CompletedTask);

        var handler = new CreatePurchaseOrderCommandHandler(_poRepoMock.Object, _tenantServiceMock.Object, _unitOfWorkMock.Object);

        var id = await handler.Handle(NewCreateCommand(), CancellationToken.None);

        Assert.NotNull(saved);
        Assert.Equal(id, saved!.Id);
        Assert.Equal(PurchaseOrderStatus.PendingApproval, saved.Status);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_WithSubmitFalse_ShouldStayDraft()
    {
        PurchaseOrder? saved = null;
        _poRepoMock.Setup(x => x.AddAsync(It.IsAny<PurchaseOrder>(), It.IsAny<CancellationToken>()))
            .Callback<PurchaseOrder, CancellationToken>((po, _) => saved = po)
            .Returns(Task.CompletedTask);

        var handler = new CreatePurchaseOrderCommandHandler(_poRepoMock.Object, _tenantServiceMock.Object, _unitOfWorkMock.Object);

        await handler.Handle(NewCreateCommand(submit: false), CancellationToken.None);

        Assert.NotNull(saved);
        Assert.Equal(PurchaseOrderStatus.Draft, saved!.Status);
    }

    [Fact]
    public async Task Submit_DraftOrder_ShouldMoveToPendingApproval()
    {
        var po = NewOrder();
        _poRepoMock.Setup(x => x.GetByIdWithItemsAsync(po.Id, It.IsAny<CancellationToken>())).ReturnsAsync(po);

        var handler = new SubmitPurchaseOrderCommandHandler(_poRepoMock.Object, _unitOfWorkMock.Object);

        await handler.Handle(new SubmitPurchaseOrderCommand(po.Id), CancellationToken.None);

        Assert.Equal(PurchaseOrderStatus.PendingApproval, po.Status);
        _poRepoMock.Verify(x => x.UpdateAsync(po, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Submit_UnknownOrder_ShouldThrowNotFound()
    {
        _poRepoMock.Setup(x => x.GetByIdWithItemsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PurchaseOrder?)null);

        var handler = new SubmitPurchaseOrderCommandHandler(_poRepoMock.Object, _unitOfWorkMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new SubmitPurchaseOrderCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Submit_PendingOrder_ShouldThrowAndNotSave()
    {
        var po = NewOrder();
        po.SubmitForApproval();
        _poRepoMock.Setup(x => x.GetByIdWithItemsAsync(po.Id, It.IsAny<CancellationToken>())).ReturnsAsync(po);

        var handler = new SubmitPurchaseOrderCommandHandler(_poRepoMock.Object, _unitOfWorkMock.Object);

        await Assert.ThrowsAsync<InventoryDomainException>(() =>
            handler.Handle(new SubmitPurchaseOrderCommand(po.Id), CancellationToken.None));
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Cancel_ApprovedOrder_ShouldCancel()
    {
        var po = NewOrder();
        po.SubmitForApproval();
        po.Approve("manager");
        _poRepoMock.Setup(x => x.GetByIdAsync(po.Id, It.IsAny<CancellationToken>())).ReturnsAsync(po);

        var handler = new CancelPurchaseOrderCommandHandler(_poRepoMock.Object, _unitOfWorkMock.Object);

        await handler.Handle(new CancelPurchaseOrderCommand(po.Id, "Supplier cannot deliver"), CancellationToken.None);

        Assert.Equal(PurchaseOrderStatus.Cancelled, po.Status);
        Assert.Contains("Supplier cannot deliver", po.Notes, StringComparison.Ordinal);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Cancel_FulfilledOrder_ShouldThrowAndNotSave()
    {
        var po = NewOrder();
        po.SubmitForApproval();
        po.Approve("manager");
        po.Fulfill();
        _poRepoMock.Setup(x => x.GetByIdAsync(po.Id, It.IsAny<CancellationToken>())).ReturnsAsync(po);

        var handler = new CancelPurchaseOrderCommandHandler(_poRepoMock.Object, _unitOfWorkMock.Object);

        await Assert.ThrowsAsync<InventoryDomainException>(() =>
            handler.Handle(new CancelPurchaseOrderCommand(po.Id, null), CancellationToken.None));
        Assert.Equal(PurchaseOrderStatus.Fulfilled, po.Status);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
