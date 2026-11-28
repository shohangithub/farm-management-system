using Farm360.Application.Feeding.Commands.DailyFeedingEntries;
using Farm360.Application.Finance.Commands;
using Farm360.Application.Finance.Queries;
using Farm360.Application.Farms.Commands;
using Farm360.Application.Health.Commands.MedicalTreatments;
using Farm360.Application.Inventory.Commands.InventoryItems;
using Farm360.Application.Inventory.Commands.StockTransactions;
using Farm360.Application.Inventory.Commands.Suppliers;
using Farm360.Application.Livestock.Commands;
using Farm360.Application.Organizations.Branches.Commands;
using Farm360.Contracts.Finance;
using Farm360.Domain.Farms.Enums;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Livestock.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Farm360.Application.IntegrationTests.Scenario;

/// <summary>
/// One fiscal year (1 Jul 2025 – 30 Jun 2026) of a small cattle-fattening partnership, driven
/// through the real MediatR commands. Every figure it produces is worked out by hand in
/// <see cref="FarmYearExpected"/>.
/// </summary>
internal sealed class FarmYearScenario(ScenarioHost host)
{
    public static readonly DateOnly BooksStart = new(2025, 6, 30);
    public static readonly DateOnly YearStart = new(2025, 7, 1);
    public static readonly DateOnly YearEnd = new(2026, 6, 30);

    public Guid FarmId { get; private set; }
    public Guid CashAccountId { get; private set; }
    public Guid BankAccountId { get; private set; }
    public Guid PartnerA { get; private set; }
    public Guid PartnerB { get; private set; }
    public Guid A1 { get; private set; }
    public Guid A2 { get; private set; }
    public Guid A3 { get; private set; }
    public Guid A4 { get; private set; }
    public Guid FeedItem { get; private set; }
    public Guid MedicineItem { get; private set; }
    public Guid LoanId { get; private set; }
    public Guid BatchId { get; private set; }

    private Guid _tenant;

    private static DateTime At(int y, int m, int d) => new(y, m, d, 0, 0, 0, DateTimeKind.Unspecified);

    public async Task RunAsync()
    {
        await SetupAsync();

        // 1 Jul 2025 — partner A puts 300,000 into the bank; a shed fan & chopper set (24,000) is bought from the bank.
        var a = await host.SendAsync(new CreateInvestorCommand(_tenant, FarmId, "Partner A", 300_000m, At(2025, 7, 1),
            ReferenceId: "PA-1", MoneyAccountId: BankAccountId));
        PartnerA = a.Id;
        await host.SendAsync(new CreateFixedAssetCommand(_tenant, FarmId, new SaveFixedAssetRequest(
            "Chopper & fans", "MachineryAndEquipment", "Cash", At(2025, 7, 1), 24_000m, "StraightLine", 0m, 2m, 0m,
            MoneyAccountId: BankAccountId)));

        // 5 Jul — 50,000 drawn from the bank into cash in hand.
        await host.SendAsync(new TransferBetweenAccountsCommand(_tenant, FarmId,
            new TransferBetweenAccountsRequest(BankAccountId, CashAccountId, 50_000m, At(2025, 7, 5))));

        // 10 Jul — A1 bought for 60,000 from the bank; A2 for 80,000 on credit, 30,000 paid now in cash.
        A1 = await BuyAsync("A-001", new DateOnly(2025, 7, 10), 60_000m, moneyAccountId: BankAccountId);
        A2 = await BuyAsync("A-002", new DateOnly(2025, 7, 10), 80_000m,
            credit: new CreditTermsRequest("Haat trader", DueDate: new DateOnly(2025, 9, 10), PaidNowBdt: 30_000m, PaymentMethod: "Cash",
                MoneyAccountId: CashAccountId));

        // 15 Jul — 1,000 kg feed at 30 (cash, from the bank); 10 medicine at 500 on credit.
        await host.SendAsync(new RecordStockInCommand(FarmId, FeedItem, 1_000m, 30m, new DateOnly(2025, 7, 15),
            InvoiceNumber: "F-1", MoneyAccountId: BankAccountId));
        await host.SendAsync(new RecordStockInCommand(FarmId, MedicineItem, 10m, 500m, new DateOnly(2025, 7, 15),
            InvoiceNumber: "M-1", Credit: new CreditTermsRequest("Vet Pharma", DueDate: new DateOnly(2025, 8, 15))));

        // 1 Aug — a 100,000 loan at 10% flat into the bank; A1 and A2 fed 10 kg each.
        LoanId = (await host.SendAsync(new CreateLoanRecordCommand(_tenant, FarmId, "Krishi Bank", 100_000m, 10m, At(2025, 8, 1),
            "Monthly", null, BankAccountId))).Id;
        await FeedAsync(new DateOnly(2025, 8, 1), A1, A2);

        // 15 Aug — A1 treated with 2 units of medicine from stock (1,000).
        await host.SendAsync(new LogMedicalTreatmentCommand(A1, "Fever", "Oxytet", 2m, "unit", 0, 0, new DateOnly(2025, 8, 15), 1_000m,
            "Dr. Vet", null, MedicineItem, 2m));

        // 31 Aug — labour 12,000 cash.  1 Sep — A2's balance 50,000 paid from the bank.  30 Sep — utility 3,000 from the bank.
        await host.SendAsync(new RecordExpenseCommand(_tenant, FarmId, "LaborCost", 12_000m, At(2025, 8, 31), "Wages Aug", "W-8", "",
            null, null, null, MoneyAccountId: CashAccountId));
        // August's wages shared out over the animals on the farm in August (A1 and A2, 6,000 each).
        await host.SendAsync(new AllocateOverheadCommand(FarmId, new DateOnly(2025, 8, 1), new DateOnly(2025, 8, 31)));
        await PayBillAsync(A2, TradeBillKind.Payable, 50_000m, new DateOnly(2025, 9, 1), BankAccountId);
        await host.SendAsync(new RecordExpenseCommand(_tenant, FarmId, "Utilities", 3_000m, At(2025, 9, 30), "Electricity", "E-9", "",
            null, null, null, MoneyAccountId: BankAccountId));

        // 1 Jan 2026 — partner B puts 200,000 into cash in hand.  10 Jan — A3 (50,000) paid directly by partner B.
        var b = await host.SendAsync(new CreateInvestorCommand(_tenant, FarmId, "Partner B", 200_000m, At(2026, 1, 1),
            ReferenceId: "PB-1", MoneyAccountId: CashAccountId));
        PartnerB = b.Id;
        A3 = await BuyAsync("A-003", new DateOnly(2026, 1, 10), 50_000m, paidByPartnerId: PartnerB);

        // 1 Feb — loan instalment 22,000 from the bank (20,000 principal + 2,000 interest); A1–A3 fed.
        await host.SendAsync(new RecordLoanRepaymentCommand(_tenant, FarmId, LoanId, 22_000m, At(2026, 2, 1), "L-1", "",
            BankAccountId));
        await FeedAsync(new DateOnly(2026, 2, 1), A1, A2, A3);

        // 15 Feb — A4 bought for 40,000 cash.  20 Feb — A1–A4 fed.
        A4 = await BuyAsync("A-004", new DateOnly(2026, 2, 15), 40_000m, moneyAccountId: CashAccountId);
        await FeedAsync(new DateOnly(2026, 2, 20), A1, A2, A3, A4);

        // 1 Mar — A1 sold for 120,000 into the bank.  15 Mar — A2 sold for 150,000 on credit, 40,000 received in cash.
        await host.SendAsync(new SellAnimalCommand(A1, 120_000m, new DateOnly(2026, 3, 1), "Buyer One", 420m, MoneyAccountId: BankAccountId));
        await host.SendAsync(new SellAnimalCommand(A2, 150_000m, new DateOnly(2026, 3, 15), "Buyer Two", 480m,
            new CreditTermsRequest("Buyer Two", DueDate: new DateOnly(2026, 4, 30), PaidNowBdt: 40_000m, PaymentMethod: "Cash",
                MoneyAccountId: CashAccountId)));

        // 1 Apr — A3 dies.  15 Apr — 30,000 collected from Buyer Two into the bank.
        await host.SendAsync(new RecordAnimalDeathCommand(A3, DisposalReason.Disease, new DateOnly(2026, 4, 1), "Sudden illness"));
        await PayBillAsync(A2, TradeBillKind.Receivable, 30_000m, new DateOnly(2026, 4, 15), BankAccountId);
    }

    /// <summary>The farm, its books (opening cash 10,000 / owner's capital 10,000), accounts and master data only.</summary>
    public async Task SetupAsync()
    {
        _tenant = host.TenantId;
        var branchId = await host.SendAsync(new CreateBranchCommand(host.OrganizationId, "BR-1", "Main branch", "branch@scenario.test",
            null, null, "Savar", "Dhaka", "Bangladesh", null, null, null, null, null, true));
        FarmId = await host.SendAsync(new CreateFarmCommand(branchId, "SF-1", "Scenario Farm", FarmType.Mixed, 1, 1, null, null, null, 50,
            null, null, null));

        await host.SendAsync(new SaveAccountingSettingsCommand(_tenant, FarmId,
            new SaveAccountingSettingsRequest(7, BooksStart.ToDateTime(TimeOnly.MinValue))));
        await host.SendAsync(new CreateBalanceSheetEntryCommand(_tenant, FarmId,
            new SaveBalanceSheetEntryRequest("Opening", BooksStart.ToDateTime(TimeOnly.MinValue), "1010", 10_000m, "Opening cash")));
        await host.SendAsync(new CreateBalanceSheetEntryCommand(_tenant, FarmId,
            new SaveBalanceSheetEntryRequest("Opening", BooksStart.ToDateTime(TimeOnly.MinValue), "3200", 10_000m, "Owner's capital")));

        var accounts = await host.SendAsync(new GetMoneyAccountsQuery(_tenant, FarmId));
        CashAccountId = accounts.Single(x => x.IsDefault).Id;
        BankAccountId = (await host.SendAsync(new CreateMoneyAccountCommand(_tenant, FarmId,
            new SaveMoneyAccountRequest("City Bank", "Bank", "City Bank", "0001")))).Id;

        _breed = (await host.SendAsync(new CreateBreedCommand("Scenario Cross", "Test breed", "Crossbred", "Bangladesh", "Beef", "Fattening",
            0.4m, 0.6m, 0.8m, 1.0m, 0.6m, 0.9m, 6m, 8m, 0m, 0m, 0m, 0m))).Id;
        await host.SendAsync(new CreateSupplierCommand("Feed House"));
        FeedItem = await host.SendAsync(new CreateInventoryItemCommand(FarmId, "Concentrate mix", InventoryCategory.Feed, "kg", 10m));
        MedicineItem = await host.SendAsync(new CreateInventoryItemCommand(FarmId, "Oxytet vial", InventoryCategory.Medicine, "unit", 1m));
        BatchId = await host.SendAsync(new CreateBatchCommand(FarmId, "Eid 2026", null));
    }

    private Guid _breed;

    private async Task<Guid> BuyAsync(string tag, DateOnly date, decimal price, Guid? moneyAccountId = null, Guid? paidByPartnerId = null,
        CreditTermsRequest? credit = null)
    {
        var dto = await host.SendAsync(new RegisterAnimalCommand(FarmId, tag, TagType.EarTag, AnimalSpecies.CattleBeef, _breed, AnimalSex.Male,
            date.AddYears(-2), AcquisitionType.Purchased, date, price, null, null, null, SkipHealthProgramme: true,
            MoneyAccountId: moneyAccountId, PaidByPartnerId: paidByPartnerId, Credit: credit));
        await host.SendAsync(new AssignAnimalsToBatchCommand(BatchId, [dto.Id]));
        return dto.Id;
    }

    private Task<ManualFeedingResult> FeedAsync(DateOnly day, params Guid[] animals) =>
        host.SendAsync(new RecordManualFeedingCommand(FarmId, day, animals, null, FeedItem, 10m, "Scenario ration"));

    public async Task<Guid> BillOfAsync(Guid sourceDocumentId, TradeBillKind kind) =>
        await host.InScopeAsync(async sp =>
        {
            var bills = await sp.GetRequiredService<ITradeBillRepository>().GetByFarmIdAsync(FarmId, kind, cancellationToken: default);
            return bills.Single(x => x.SourceDocumentId == sourceDocumentId).Id;
        });

    private async Task PayBillAsync(Guid sourceDocumentId, TradeBillKind kind, decimal amount, DateOnly date, Guid accountId)
    {
        var billId = await BillOfAsync(sourceDocumentId, kind);
        await host.SendAsync(new RecordBillPaymentCommand(FarmId, billId, new RecordBillPaymentRequest(amount, date, "Bank", MoneyAccountId: accountId)));
    }
}
