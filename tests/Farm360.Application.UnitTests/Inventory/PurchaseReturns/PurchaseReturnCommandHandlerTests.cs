using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Inventory.Commands.PurchaseReturns;
using Farm360.Application.Inventory.DTOs;
using Farm360.Application.Inventory.EventHandlers;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Exceptions;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using MediatR;
using Moq;
using Xunit;

namespace Farm360.Application.UnitTests.Inventory.PurchaseReturns;

public class PurchaseReturnCommandHandlerTests
{
    private readonly Mock<IPurchaseReturnRepository> _returnRepoMock = new();
    private readonly Mock<IPurchaseOrderRepository> _poRepoMock = new();
    private readonly Mock<IInventoryItemRepository> _itemRepoMock = new();
    private readonly Mock<IStockTransactionRepository> _stockTxRepoMock = new();
    private readonly Mock<ITenantService> _tenantServiceMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IPublisher> _publisherMock = new();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _farmId = Guid.NewGuid();
    private readonly Guid _supplierId = Guid.NewGuid();

    public PurchaseReturnCommandHandlerTests()
    {
        _tenantServiceMock.Setup(x => x.TenantId).Returns(_tenantId);
        _currentUserServiceMock.Setup(x => x.UserId).Returns(Guid.NewGuid());
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    [Fact]
    public async Task CreatePurchaseReturn_WithValidFulfilledPo_ShouldSucceed()
    {
        // Arrange
        var po = new PurchaseOrder(Guid.NewGuid(), _tenantId, _farmId, _supplierId, DateOnly.FromDateTime(DateTime.UtcNow));
        var item = new InventoryItem(Guid.NewGuid(), _tenantId, _farmId, "Feed X", "FX-01", InventoryCategory.Feed, "kg", 10, 100, 50m);
        po.AddItem(item.Id, 50, 50m);
        po.SubmitForApproval();
        po.Approve("manager");
        po.Fulfill();

        var poItem = po.Items.First();

        _poRepoMock.Setup(x => x.GetByIdWithItemsAsync(po.Id, It.IsAny<CancellationToken>())).ReturnsAsync(po);
        _itemRepoMock.Setup(x => x.GetByIdAsync(item.Id, It.IsAny<CancellationToken>())).ReturnsAsync(item);
        _returnRepoMock.Setup(x => x.GetReturnedQuantitiesByPoAsync(po.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, decimal>());

        var handler = new CreatePurchaseReturnCommandHandler(
            _returnRepoMock.Object, _poRepoMock.Object, _itemRepoMock.Object, _tenantServiceMock.Object, _unitOfWorkMock.Object);

        var command = new CreatePurchaseReturnCommand(
            _farmId, po.Id, DateOnly.FromDateTime(DateTime.UtcNow),
            PurchaseReturnReason.Damaged, "CN-001", "Damaged goods",
            new List<CreatePurchaseReturnItemRequest>
            {
                new(poItem.Id, 10)
            });

        // Act
        var resultId = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, resultId);
        _returnRepoMock.Verify(x => x.AddAsync(It.Is<PurchaseReturn>(r => r.PurchaseOrderId == po.Id && r.TotalAmountBdt == 500m), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreatePurchaseReturn_WhenPoNotFulfilled_ShouldThrowInventoryDomainException()
    {
        // Arrange - PO in Draft status
        var po = new PurchaseOrder(Guid.NewGuid(), _tenantId, _farmId, _supplierId, DateOnly.FromDateTime(DateTime.UtcNow));
        po.AddItem(Guid.NewGuid(), 50, 50m);

        _poRepoMock.Setup(x => x.GetByIdWithItemsAsync(po.Id, It.IsAny<CancellationToken>())).ReturnsAsync(po);

        var handler = new CreatePurchaseReturnCommandHandler(
            _returnRepoMock.Object, _poRepoMock.Object, _itemRepoMock.Object, _tenantServiceMock.Object, _unitOfWorkMock.Object);

        var command = new CreatePurchaseReturnCommand(
            _farmId, po.Id, DateOnly.FromDateTime(DateTime.UtcNow),
            PurchaseReturnReason.Damaged, null, null,
            new List<CreatePurchaseReturnItemRequest> { new(po.Items.First().Id, 10) });

        // Act & Assert
        await Assert.ThrowsAsync<InventoryDomainException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task CreatePurchaseReturn_WhenQuantityExceedsAvailableStock_ShouldThrowInventoryDomainException()
    {
        // Arrange
        var po = new PurchaseOrder(Guid.NewGuid(), _tenantId, _farmId, _supplierId, DateOnly.FromDateTime(DateTime.UtcNow));
        var item = new InventoryItem(Guid.NewGuid(), _tenantId, _farmId, "Feed X", "FX-01", InventoryCategory.Feed, "kg", 10, 5, 50m); // stock is only 5
        po.AddItem(item.Id, 50, 50m);
        po.SubmitForApproval();
        po.Approve("manager");
        po.Fulfill();

        _poRepoMock.Setup(x => x.GetByIdWithItemsAsync(po.Id, It.IsAny<CancellationToken>())).ReturnsAsync(po);
        _itemRepoMock.Setup(x => x.GetByIdAsync(item.Id, It.IsAny<CancellationToken>())).ReturnsAsync(item);
        _returnRepoMock.Setup(x => x.GetReturnedQuantitiesByPoAsync(po.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, decimal>());

        var handler = new CreatePurchaseReturnCommandHandler(
            _returnRepoMock.Object, _poRepoMock.Object, _itemRepoMock.Object, _tenantServiceMock.Object, _unitOfWorkMock.Object);

        var command = new CreatePurchaseReturnCommand(
            _farmId, po.Id, DateOnly.FromDateTime(DateTime.UtcNow),
            PurchaseReturnReason.Damaged, null, null,
            new List<CreatePurchaseReturnItemRequest> { new(po.Items.First().Id, 20) }); // wants to return 20, but stock is 5

        // Act & Assert
        await Assert.ThrowsAsync<InventoryDomainException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task CompletePurchaseReturn_ShouldDeductStockAndPublishNotification()
    {
        // Arrange
        var po = new PurchaseOrder(Guid.NewGuid(), _tenantId, _farmId, _supplierId, DateOnly.FromDateTime(DateTime.UtcNow));
        var item = new InventoryItem(Guid.NewGuid(), _tenantId, _farmId, "Feed X", "FX-01", InventoryCategory.Feed, "kg", 10, 100, 50m);
        po.AddItem(item.Id, 50, 50m);

        var purchaseReturn = new PurchaseReturn(
            Guid.NewGuid(), _tenantId, _farmId, po.Id, _supplierId,
            DateOnly.FromDateTime(DateTime.UtcNow), PurchaseReturnReason.Damaged);
        purchaseReturn.AddItem(po.Items.First().Id, item.Id, 10, 50m);

        _returnRepoMock.Setup(x => x.GetByIdWithItemsAsync(purchaseReturn.Id, It.IsAny<CancellationToken>())).ReturnsAsync(purchaseReturn);
        _poRepoMock.Setup(x => x.GetByIdWithItemsAsync(po.Id, It.IsAny<CancellationToken>())).ReturnsAsync(po);
        _itemRepoMock.Setup(x => x.GetByIdAsync(item.Id, It.IsAny<CancellationToken>())).ReturnsAsync(item);

        var handler = new CompletePurchaseReturnCommandHandler(
            _returnRepoMock.Object, _poRepoMock.Object, _itemRepoMock.Object,
            _stockTxRepoMock.Object, _currentUserServiceMock.Object, _unitOfWorkMock.Object, _publisherMock.Object);

        // Act
        await handler.Handle(new CompletePurchaseReturnCommand(purchaseReturn.Id), CancellationToken.None);

        // Assert
        Assert.Equal(PurchaseReturnStatus.Completed, purchaseReturn.Status);
        Assert.Equal(90m, item.CurrentStock); // 100 - 10
        _stockTxRepoMock.Verify(x => x.AddAsync(It.Is<StockTransaction>(tx => tx.TransactionType == StockTransactionType.PurchaseReturn && tx.Quantity == 10m), It.IsAny<CancellationToken>()), Times.Once);
        _publisherMock.Verify(x => x.Publish(It.IsAny<PurchaseReturnCompletedNotification>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
