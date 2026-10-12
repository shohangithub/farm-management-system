using System;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using FluentAssertions;
using Xunit;

namespace Farm360.Domain.UnitTests.Finance;

public class TradeBillTests
{
    private static readonly DateOnly BillDate = new(2026, 9, 1);

    private static TradeBill Bill(decimal amount = 10_000m) => TradeBill.Create(
        Guid.NewGuid(), Guid.NewGuid(), TradeBillKind.Payable, "Rahim Feeds", Guid.NewGuid(),
        TransactionCategory.InventoryPurchase, amount, BillDate, BillDate.AddDays(30), "PO-1", "Feed");

    [Fact]
    public void Status_FollowsPaymentsAndAdjustments()
    {
        var bill = Bill();
        bill.Status.Should().Be(TradeBillStatus.Open);

        bill.ApplyPayment(4_000m);
        bill.Status.Should().Be(TradeBillStatus.PartiallyPaid);

        bill.Adjust(1_000m);
        bill.OutstandingBdt.Should().Be(5_000m);

        bill.ApplyPayment(5_000m);
        bill.Status.Should().Be(TradeBillStatus.Paid);
        bill.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void Overpaying_IsRejected()
    {
        var bill = Bill();

        var act = () => bill.ApplyPayment(10_000.50m);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ReducingBelowWhatWasPaid_ShowsOverpaid()
    {
        var bill = Bill();
        bill.ApplyPayment(6_000m);

        bill.Revise(5_000m, BillDate);

        bill.Status.Should().Be(TradeBillStatus.Overpaid);
        bill.OutstandingBdt.Should().Be(-1_000m);
    }

    [Fact]
    public void Cancel_IsOnlyAllowedBeforeAnythingIsSettled()
    {
        var paid = Bill();
        paid.ApplyPayment(100m);
        paid.Invoking(b => b.Cancel()).Should().Throw<InvalidOperationException>();

        var unpaid = Bill();
        unpaid.Cancel();
        unpaid.Status.Should().Be(TradeBillStatus.Cancelled);
        unpaid.Invoking(b => b.ApplyPayment(100m)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Create_RequiresAPartyAndASensibleDueDate()
    {
        var noParty = () => TradeBill.Create(Guid.NewGuid(), Guid.NewGuid(), TradeBillKind.Receivable, " ", Guid.NewGuid(),
            TransactionCategory.AnimalSale, 1m, BillDate, null, "A-1", "Sale");
        var dueBeforeBill = () => TradeBill.Create(Guid.NewGuid(), Guid.NewGuid(), TradeBillKind.Receivable, "Buyer", Guid.NewGuid(),
            TransactionCategory.AnimalSale, 1m, BillDate, BillDate.AddDays(-1), "A-1", "Sale");

        noParty.Should().Throw<ArgumentException>();
        dueBeforeBill.Should().Throw<ArgumentException>();
    }
}
