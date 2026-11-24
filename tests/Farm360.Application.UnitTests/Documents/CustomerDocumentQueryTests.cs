using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Documents;
using Farm360.Application.Documents.Queries;
using Farm360.Application.Reporting.Model;
using Farm360.Domain.Exceptions;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Repositories;
using Farm360.Domain.Livestock.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Farm360.Application.UnitTests.Documents;

public class CustomerDocumentQueryTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid FarmId = Guid.NewGuid();
    private static readonly DateOnly SaleDate = new(2026, 11, 1);

    private readonly IAnimalRepository _animals = Substitute.For<IAnimalRepository>();
    private readonly IBreedRepository _breeds = Substitute.For<IBreedRepository>();
    private readonly ITradeBillRepository _bills = Substitute.For<ITradeBillRepository>();
    private readonly IPurchaseOrderRepository _orders = Substitute.For<IPurchaseOrderRepository>();
    private readonly ISupplierRepository _suppliers = Substitute.For<ISupplierRepository>();
    private readonly IInventoryItemRepository _items = Substitute.For<IInventoryItemRepository>();
    private readonly IFarmAccessService _access = Substitute.For<IFarmAccessService>();
    private readonly IDocumentHeaderBuilder _headers = Substitute.For<IDocumentHeaderBuilder>();

    public CustomerDocumentQueryTests()
    {
        _access.CanAccessFarmAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        _headers.BuildAsync(Arg.Any<Guid>(), Arg.Any<string?>(), Arg.Any<bool?>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(DocumentTestKit.Header(
                string.Equals(ci.ArgAt<string?>(1), "bn", StringComparison.Ordinal) ? ReportLanguage.Bn : ReportLanguage.En,
                ci.ArgAt<bool?>(2) ?? false)));
    }

    // ── Sale invoice ────────────────────────────────────────────────────────

    private static Animal SoldAnimal(string? buyer = "Rahim Uddin", decimal? weightKg = 320m)
    {
        var animal = Animal.Create(TenantId, FarmId, AnimalTag.Create("BD-0042", TagType.Manual), AnimalSpecies.CattleBeef,
            Guid.NewGuid(), AnimalSex.Male, new DateOnly(2024, 8, 1), AcquisitionType.Purchased, new DateOnly(2025, 1, 1), 50_000m, null);
        animal.Sell(80_000m, SaleDate, Guid.NewGuid(), buyer, weightKg);
        return animal;
    }

    private GetSaleInvoiceDocumentQueryHandler SaleInvoiceHandler() => new(_animals, _breeds, _bills, _access, _headers);

    [Fact]
    public async Task Credit_sale_invoice_uses_the_bill_number_and_shows_what_is_still_due()
    {
        var animal = SoldAnimal();
        _animals.GetByIdAsync(animal.Id, Arg.Any<CancellationToken>()).Returns(animal);

        var bill = TradeBill.Create(TenantId, FarmId, TradeBillKind.Receivable, "Karim Traders", animal.Id,
            TransactionCategory.AnimalSale, 80_000m, SaleDate, new DateOnly(2026, 12, 1), "INV-0042", "Sale of BD-0042",
            partyPhone: "01811-222333");
        bill.ApplyPayment(30_000m);
        var payment = TradeBillPayment.Create(TenantId, bill.Id, FarmId, SaleDate, 30_000m, "bKash", "TX-991", null);

        _bills.FindBySourceAsync(FarmId, animal.Id, TransactionCategory.AnimalSale, Arg.Any<CancellationToken>()).Returns(bill);
        _bills.GetPaymentsAsync(bill.Id, Arg.Any<CancellationToken>()).Returns(new List<TradeBillPayment> { payment });

        var doc = await SaleInvoiceHandler().Handle(new GetSaleInvoiceDocumentQuery(animal.Id), CancellationToken.None);

        doc.InvoiceNumber.Should().Be("INV-0042");
        doc.InvoiceDate.Should().Be(SaleDate);
        doc.BuyerName.Should().Be("Karim Traders");
        doc.BuyerPhone.Should().Be("01811-222333");
        doc.IsCreditSale.Should().BeTrue();
        doc.AmountBdt.Should().Be(80_000m);
        doc.ReceivedBdt.Should().Be(30_000m);
        doc.BalanceDueBdt.Should().Be(50_000m);
        doc.DueDate.Should().Be(new DateOnly(2026, 12, 1));
        doc.Payments.Should().ContainSingle().Which.Method.Should().Be("bKash");
        doc.Line.Tag.Should().Be("BD-0042");
        doc.Line.Sex.Should().Be("Male");
        doc.Line.Species.Should().Be("Cattle (beef)");
        doc.Line.Age.Should().Be("2 y 3 m");
        doc.Line.WeightKg.Should().Be(320m);
        doc.Line.PricePerKgBdt.Should().Be(250m);
        doc.AmountInWords.Should().Be("Taka Eighty Thousand Only");
        doc.FileName.Should().Be("sale-invoice_INV-0042.pdf");
    }

    [Fact]
    public async Task Cash_sale_invoice_gets_a_stable_number_and_is_fully_received()
    {
        var animal = SoldAnimal(weightKg: null);
        _animals.GetByIdAsync(animal.Id, Arg.Any<CancellationToken>()).Returns(animal);
        _bills.FindBySourceAsync(FarmId, animal.Id, TransactionCategory.AnimalSale, Arg.Any<CancellationToken>()).Returns((TradeBill?)null);

        var doc = await SaleInvoiceHandler().Handle(new GetSaleInvoiceDocumentQuery(animal.Id), CancellationToken.None);

        doc.InvoiceNumber.Should().Be("SAL-20261101-BD-0042");
        doc.BuyerName.Should().Be("Rahim Uddin");
        doc.IsCreditSale.Should().BeFalse();
        doc.ReceivedBdt.Should().Be(80_000m);
        doc.BalanceDueBdt.Should().Be(0m);
        doc.Payments.Should().BeEmpty();
        doc.Line.WeightKg.Should().BeNull();
        doc.Line.PricePerKgBdt.Should().BeNull();
    }

    [Fact]
    public async Task Sale_invoice_in_bangla_translates_labels_and_writes_the_amount_in_bangla_words()
    {
        var animal = SoldAnimal(buyer: null);
        _animals.GetByIdAsync(animal.Id, Arg.Any<CancellationToken>()).Returns(animal);
        _bills.FindBySourceAsync(FarmId, animal.Id, TransactionCategory.AnimalSale, Arg.Any<CancellationToken>()).Returns((TradeBill?)null);

        var doc = await SaleInvoiceHandler().Handle(new GetSaleInvoiceDocumentQuery(animal.Id, "bn", true), CancellationToken.None);

        doc.Header.Language.Should().Be(ReportLanguage.Bn);
        doc.BuyerName.Should().Be("নগদ ক্রেতা");
        doc.Line.Sex.Should().Be("পুরুষ");
        doc.Line.Age.Should().Be("২ বছর ৩ মাস");
        doc.AmountInWords.Should().Be("আশি হাজার টাকা মাত্র");
    }

    [Fact]
    public async Task An_animal_that_was_not_sold_has_no_invoice()
    {
        var animal = Animal.Create(TenantId, FarmId, AnimalTag.Create("BD-0099", TagType.Manual), AnimalSpecies.Goat,
            Guid.NewGuid(), AnimalSex.Female, new DateOnly(2025, 1, 1), AcquisitionType.Purchased, new DateOnly(2025, 2, 1), 9_000m, null);
        _animals.GetByIdAsync(animal.Id, Arg.Any<CancellationToken>()).Returns(animal);

        var act = () => SaleInvoiceHandler().Handle(new GetSaleInvoiceDocumentQuery(animal.Id), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task A_user_without_access_to_the_farm_cannot_print_its_invoice()
    {
        var animal = SoldAnimal();
        _animals.GetByIdAsync(animal.Id, Arg.Any<CancellationToken>()).Returns(animal);
        _access.CanAccessFarmAsync(FarmId, Arg.Any<CancellationToken>()).Returns(false);

        var act = () => SaleInvoiceHandler().Handle(new GetSaleInvoiceDocumentQuery(animal.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
        await _headers.DidNotReceiveWithAnyArgs().BuildAsync(default, default, default, default);
    }

    [Fact]
    public async Task Unknown_animal_is_not_found()
    {
        var act = () => SaleInvoiceHandler().Handle(new GetSaleInvoiceDocumentQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── Payment receipt ─────────────────────────────────────────────────────

    private GetPaymentReceiptDocumentQueryHandler ReceiptHandler() => new(_bills, _access, _headers);

    [Fact]
    public async Task Receipt_shows_the_balance_left_once_that_payment_was_in()
    {
        var bill = TradeBill.Create(TenantId, FarmId, TradeBillKind.Receivable, "Karim Traders", Guid.NewGuid(),
            TransactionCategory.AnimalSale, 80_000m, SaleDate, null, "INV-0042", "Sale of BD-0042", partyPhone: "01811-222333");
        var first = TradeBillPayment.Create(TenantId, bill.Id, FarmId, new DateOnly(2026, 11, 5), 30_000m, "Cash", null, null);
        var second = TradeBillPayment.Create(TenantId, bill.Id, FarmId, new DateOnly(2026, 11, 10), 20_000m, "Bank", "CHQ-77", "Second instalment");
        bill.ApplyPayment(30_000m);
        bill.ApplyPayment(20_000m);

        _bills.GetByIdAsync(bill.Id, Arg.Any<CancellationToken>()).Returns(bill);
        _bills.GetPaymentByIdAsync(first.Id, Arg.Any<CancellationToken>()).Returns(first);
        _bills.GetPaymentByIdAsync(second.Id, Arg.Any<CancellationToken>()).Returns(second);
        _bills.GetPaymentsAsync(bill.Id, Arg.Any<CancellationToken>()).Returns(new List<TradeBillPayment> { first, second });

        var firstReceipt = await ReceiptHandler().Handle(new GetPaymentReceiptDocumentQuery(first.Id), CancellationToken.None);
        var secondReceipt = await ReceiptHandler().Handle(new GetPaymentReceiptDocumentQuery(second.Id), CancellationToken.None);

        firstReceipt.IsCollection.Should().BeTrue();
        firstReceipt.ReceiptNumber.Should().StartWith("MR-20261105-");
        firstReceipt.PartyName.Should().Be("Karim Traders");
        firstReceipt.AmountBdt.Should().Be(30_000m);
        firstReceipt.PaidToDateBdt.Should().Be(30_000m);
        firstReceipt.BalanceRemainingBdt.Should().Be(50_000m);
        firstReceipt.BillReference.Should().Be("INV-0042");
        firstReceipt.AmountInWords.Should().Be("Taka Thirty Thousand Only");

        secondReceipt.PaidToDateBdt.Should().Be(50_000m);
        secondReceipt.BalanceRemainingBdt.Should().Be(30_000m);
        secondReceipt.Method.Should().Be("Bank");
        secondReceipt.Reference.Should().Be("CHQ-77");
        secondReceipt.Notes.Should().Be("Second instalment");
        secondReceipt.FileName.Should().StartWith("money-receipt_MR-20261110-").And.EndWith(".pdf");
    }

    [Fact]
    public async Task Supplier_payment_prints_as_a_payment_voucher()
    {
        var bill = TradeBill.Create(TenantId, FarmId, TradeBillKind.Payable, "Agro Feeds Ltd", Guid.NewGuid(),
            TransactionCategory.InventoryPurchase, 12_000m, SaleDate, null, "PO-17", "Wheat bran");
        var payment = TradeBillPayment.Create(TenantId, bill.Id, FarmId, SaleDate, 12_000m, "Nagad", null, null);
        bill.ApplyPayment(12_000m);

        _bills.GetByIdAsync(bill.Id, Arg.Any<CancellationToken>()).Returns(bill);
        _bills.GetPaymentByIdAsync(payment.Id, Arg.Any<CancellationToken>()).Returns(payment);
        _bills.GetPaymentsAsync(bill.Id, Arg.Any<CancellationToken>()).Returns(new List<TradeBillPayment> { payment });

        var receipt = await ReceiptHandler().Handle(new GetPaymentReceiptDocumentQuery(payment.Id, "bn"), CancellationToken.None);

        receipt.IsCollection.Should().BeFalse();
        receipt.ReceiptNumber.Should().StartWith("PV-20261101-");
        receipt.BalanceRemainingBdt.Should().Be(0m);
        receipt.AmountInWords.Should().Be("বারো হাজার টাকা মাত্র");
    }

    [Fact]
    public async Task Receipt_of_another_farm_is_refused()
    {
        var payment = TradeBillPayment.Create(TenantId, Guid.NewGuid(), FarmId, SaleDate, 1_000m, "Cash", null, null);
        _bills.GetPaymentByIdAsync(payment.Id, Arg.Any<CancellationToken>()).Returns(payment);
        _access.CanAccessFarmAsync(FarmId, Arg.Any<CancellationToken>()).Returns(false);

        var act = () => ReceiptHandler().Handle(new GetPaymentReceiptDocumentQuery(payment.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    // ── Purchase order ──────────────────────────────────────────────────────

    [Fact]
    public async Task Purchase_order_lists_items_by_name_in_the_unit_they_were_ordered_in()
    {
        var supplier = new Supplier(Guid.NewGuid(), TenantId, "Agro Feeds Ltd", "Mr. Hasan", "01911-000111", "sales@agrofeeds.com", "Tongi, Gazipur");
        var bran = new InventoryItem(Guid.NewGuid(), TenantId, FarmId, "Wheat bran", "WB-01", InventoryCategory.Feed, "kg", 100m);
        var vaccine = new InventoryItem(Guid.NewGuid(), TenantId, FarmId, "FMD vaccine", "VAC-FMD", InventoryCategory.Vaccine, "bottle", 1m);

        var order = new PurchaseOrder(Guid.NewGuid(), TenantId, FarmId, supplier.Id, new DateOnly(2026, 11, 20), new DateOnly(2026, 11, 27), "Deliver before Friday");
        order.AddItem(bran.Id, 500m, 40m, "Bag (50 kg)", 10m);
        order.AddItem(vaccine.Id, 2m, 350m);

        _orders.GetByIdWithItemsAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        _suppliers.GetByIdAsync(supplier.Id, Arg.Any<CancellationToken>()).Returns(supplier);
        // The farm list misses the vaccine (e.g. archived); the handler fetches it on its own.
        _items.GetByFarmIdAsync(FarmId, Arg.Any<InventoryCategory?>(), Arg.Any<CancellationToken>())
            .Returns(new List<InventoryItem> { bran });
        _items.GetByIdAsync(vaccine.Id, Arg.Any<CancellationToken>()).Returns(vaccine);

        var handler = new GetPurchaseOrderDocumentQueryHandler(_orders, _suppliers, _items, _access, _headers);
        var doc = await handler.Handle(new GetPurchaseOrderDocumentQuery(order.Id), CancellationToken.None);

        doc.PoNumber.Should().Be(order.PoNumber);
        doc.SupplierName.Should().Be("Agro Feeds Ltd");
        doc.SupplierPhone.Should().Be("01911-000111");
        doc.SupplierAddress.Should().Be("Tongi, Gazipur");
        doc.ExpectedDeliveryDate.Should().Be(new DateOnly(2026, 11, 27));
        doc.Status.Should().Be("Draft");
        doc.Notes.Should().Be("Deliver before Friday");
        doc.Lines.Should().HaveCount(2);

        doc.Lines[0].ItemName.Should().Be("Wheat bran");
        doc.Lines[0].Quantity.Should().Be(10m);
        doc.Lines[0].Unit.Should().Be("Bag (50 kg)");
        doc.Lines[0].UnitCostBdt.Should().Be(2_000m);
        doc.Lines[0].TotalBdt.Should().Be(20_000m);

        doc.Lines[1].ItemName.Should().Be("FMD vaccine");
        doc.Lines[1].Quantity.Should().Be(2m);
        doc.Lines[1].Unit.Should().Be("bottle");
        doc.Lines[1].TotalBdt.Should().Be(700m);

        doc.GrandTotalBdt.Should().Be(20_700m);
        doc.AmountInWords.Should().Be("Taka Twenty Thousand Seven Hundred Only");
        doc.FileName.Should().StartWith("purchase-order_").And.EndWith(".pdf");
    }

    [Fact]
    public async Task Purchase_order_status_is_printed_in_bangla()
    {
        var order = new PurchaseOrder(Guid.NewGuid(), TenantId, FarmId, Guid.NewGuid(), new DateOnly(2026, 11, 20));
        _orders.GetByIdWithItemsAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        _items.GetByFarmIdAsync(FarmId, Arg.Any<InventoryCategory?>(), Arg.Any<CancellationToken>())
            .Returns(new List<InventoryItem>());

        var handler = new GetPurchaseOrderDocumentQueryHandler(_orders, _suppliers, _items, _access, _headers);
        var doc = await handler.Handle(new GetPurchaseOrderDocumentQuery(order.Id, "bn"), CancellationToken.None);

        doc.Status.Should().Be("খসড়া");
        doc.Lines.Should().BeEmpty();
        doc.SupplierName.Should().Be("—");
    }
}
