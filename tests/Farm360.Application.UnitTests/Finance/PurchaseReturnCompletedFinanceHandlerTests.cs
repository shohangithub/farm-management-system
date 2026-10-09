using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.EventHandlers.Integration;
using Farm360.Application.Finance.Repositories;
using Farm360.Application.Inventory.EventHandlers;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Inventory.Events;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Farm360.Application.UnitTests.Finance;

public class PurchaseReturnCompletedFinanceHandlerTests
{
    private readonly Mock<IFinancialTransactionRepository> _financeRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ILogger<PurchaseReturnCompletedFinanceHandler>> _loggerMock = new();

    public PurchaseReturnCompletedFinanceHandlerTests()
    {
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    [Fact]
    public async Task Handle_ShouldPostFinancialTransaction_WhenReturnAmountIsPositive()
    {
        // Arrange
        var domainEvent = new PurchaseReturnCompletedEvent(
            PurchaseReturnId: Guid.NewGuid(),
            TenantId: Guid.NewGuid(),
            FarmId: Guid.NewGuid(),
            PurchaseOrderId: Guid.NewGuid(),
            SupplierId: Guid.NewGuid(),
            ReturnNumber: "PR-20261007-001",
            TotalAmountBdt: 1500.50m,
            ReturnDate: DateOnly.FromDateTime(DateTime.UtcNow));

        var notification = new PurchaseReturnCompletedNotification(domainEvent);
        var handler = new PurchaseReturnCompletedFinanceHandler(_financeRepoMock.Object, _unitOfWorkMock.Object, _loggerMock.Object);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        _financeRepoMock.Verify(x => x.AddAsync(
            It.Is<FinancialTransaction>(tx =>
                tx.AmountBdt == 1500.50m &&
                tx.Type == TransactionType.Income &&
                tx.Category == TransactionCategory.InventoryPurchaseReturn &&
                tx.ReferenceId == "PR-20261007-001"),
            It.IsAny<CancellationToken>()), Times.Once);

        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldDoNothing_WhenReturnAmountIsZero()
    {
        // Arrange
        var domainEvent = new PurchaseReturnCompletedEvent(
            PurchaseReturnId: Guid.NewGuid(),
            TenantId: Guid.NewGuid(),
            FarmId: Guid.NewGuid(),
            PurchaseOrderId: Guid.NewGuid(),
            SupplierId: Guid.NewGuid(),
            ReturnNumber: "PR-20261007-002",
            TotalAmountBdt: 0m,
            ReturnDate: DateOnly.FromDateTime(DateTime.UtcNow));

        var notification = new PurchaseReturnCompletedNotification(domainEvent);
        var handler = new PurchaseReturnCompletedFinanceHandler(_financeRepoMock.Object, _unitOfWorkMock.Object, _loggerMock.Object);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        _financeRepoMock.Verify(x => x.AddAsync(It.IsAny<FinancialTransaction>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
