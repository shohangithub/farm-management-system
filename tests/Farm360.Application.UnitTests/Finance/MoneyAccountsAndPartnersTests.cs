using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Common.Localization;
using Farm360.Application.Finance.Commands;
using Farm360.Application.Finance.Queries;
using Farm360.Application.Finance.Services;
using Farm360.Application.Livestock.Commands;
using Farm360.Application.Reporting.Definitions.Finance;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Accounting;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Repositories;
using Farm360.Domain.Tenancy;
using Farm360.Domain.Tenancy.Repositories;
using FluentAssertions;
using MediatR;
using NSubstitute;
using Xunit;

namespace Farm360.Application.UnitTests.Finance;

/// <summary>
/// Money accounts (cash in hand, bank, wallet) and partner capital: balances, transfers, the
/// "paid from" choice on every money-moving form, spends paid directly by a partner, and the
/// time-weighted (capital-days) profit split.
/// </summary>
public class MoneyAccountsAndPartnersTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid FarmId = Guid.NewGuid();
    private static readonly DateTime Day = new(2026, 9, 1);

    private readonly InMemoryFinancialTransactionRepository _ledger = new();
    private readonly InMemoryTradeBillRepository _bills = new();
    private readonly InMemoryMoneyAccountRepository _accounts = new();
    private readonly InMemoryInvestorRepository _investors = new();
    private readonly OperationWarnings _warnings = new();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly MoneyAccountService _money;
    private readonly LedgerPostingService _posting;
    private readonly TradeCreditService _credit;
    private readonly MoneyAccount _cash;
    private readonly MoneyAccount _bank;

    public MoneyAccountsAndPartnersTests()
    {
        _money = new MoneyAccountService(_accounts, _ledger, new FinancialStatementEngine(_ledger), _investors, _warnings);
        _posting = new LedgerPostingService(_ledger, _bills, _money);
        _credit = new TradeCreditService(_bills, _ledger, _posting);
        _cash = MoneyAccount.CreateDefault(TenantId, FarmId);
        _bank = MoneyAccount.Create(TenantId, FarmId, "Sonali Bank", MoneyAccountKind.Bank, "Sonali Bank", "0123");
        _accounts.Add(_cash);
        _accounts.Add(_bank);
    }

    private async Task<FinancialTransaction> Post(TransactionCategory category, decimal amount, Guid? account = null,
        SettlementAccount settlement = SettlementAccount.Cash, DateTime? date = null)
    {
        var t = FinancialTransaction.Create(TenantId, FarmId, AccountChart.For(category).Type, category, amount, date ?? Day,
            settlement: settlement, moneyAccountId: account);
        await _ledger.AddAsync(t);
        return t;
    }

    private async Task<decimal> Balance(MoneyAccount account) =>
        (await _money.GetBalancesAsync(FarmId)).Single(b => b.Account.Id == account.Id).BalanceBdt;

    private decimal LedgerCash => AccountChart.Balances(_ledger.Rows).GetValueOrDefault(AccountChart.CashAccount);

    // ── Balances ────────────────────────────────────────────────────────────

    [Fact]
    public async Task EachAccountsBalance_IsTheSumOfItsCashEffects_AndTheyAddUpToCashAndBank()
    {
        await Post(TransactionCategory.MilkSale, 10_000m);                       // no account → default (cash in hand)
        await Post(TransactionCategory.FeedCost, 3_000m, _cash.Id);
        await Post(TransactionCategory.LoanDisbursement, 50_000m, _bank.Id);
        await Post(TransactionCategory.Utilities, 20_000m, _bank.Id);
        await Post(TransactionCategory.FeedCost, 999m, settlement: SettlementAccount.Inventory); // stock used: no money moved
        await Post(TransactionCategory.Transport, 500m, settlement: SettlementAccount.Payable);   // on credit: not paid yet

        (await Balance(_cash)).Should().Be(7_000m);
        (await Balance(_bank)).Should().Be(30_000m);
        LedgerCash.Should().Be(37_000m, "the accounts are a breakdown of the one Cash & Bank line");
    }

    [Fact]
    public async Task TransferBetweenAccounts_MovesMoneyBetweenAccounts_ButNotTheFarmsCashOrProfit()
    {
        await Post(TransactionCategory.LoanDisbursement, 50_000m, _bank.Id);
        var cashBefore = LedgerCash;
        var handler = new TransferBetweenAccountsCommandHandler(_money, _ledger, _uow);

        var transfer = await handler.Handle(new TransferBetweenAccountsCommand(TenantId, FarmId,
            new TransferBetweenAccountsRequest(_bank.Id, _cash.Id, 20_000m, Day)), CancellationToken.None);

        (await Balance(_bank)).Should().Be(30_000m);
        (await Balance(_cash)).Should().Be(20_000m);
        LedgerCash.Should().Be(cashBefore, "a transfer leaves total cash unchanged");
        AccountChart.NetProfit(_ledger.Rows).Should().Be(0m, "a transfer is not income or expense");
        AccountChart.CashIn(_ledger.Rows).Should().Be(50_000m, "only the loan brought money into the farm");
        AccountChart.CashOut(_ledger.Rows).Should().Be(0m);
        var legs = _ledger.Of(TransactionCategory.AccountTransfer);
        legs.Should().HaveCount(2);
        legs.Sum(AccountChart.AccountCashEffect).Should().Be(0m, "the two legs net to zero");
        legs.Should().OnlyContain(l => l.LinkedTransactionId != null);

        var ledger = await new FinancialStatementEngine(_ledger).LoadAsync(FarmId, DateOnly.FromDateTime(Day));
        CashFlowStatement.Build(ledger, DateOnly.FromDateTime(Day), DateOnly.FromDateTime(Day)).Lines
            .Should().NotContain(l => l.Code == AccountChart.CashAccount, "transfers are not a cash flow of the farm");

        await new DeleteMoneyTransferCommandHandler(_ledger, _uow).Handle(new DeleteMoneyTransferCommand(FarmId, transfer.InTransactionId), CancellationToken.None);
        _ledger.Of(TransactionCategory.AccountTransfer).Should().BeEmpty("deleting either leg removes both");
        (await Balance(_bank)).Should().Be(50_000m);
    }

    [Fact]
    public async Task TransferOfMoreThanTheAccountHolds_Warns_ButIsNotBlocked()
    {
        await new TransferBetweenAccountsCommandHandler(_money, _ledger, _uow).Handle(new TransferBetweenAccountsCommand(TenantId, FarmId,
            new TransferBetweenAccountsRequest(_bank.Id, _cash.Id, 5_000m, Day)), CancellationToken.None);

        _warnings.Messages.Should().ContainSingle().Which.Should().Contain("Sonali Bank has ৳0; this spends ৳5,000");
        (await Balance(_bank)).Should().Be(-5_000m);
    }

    // ── "Paid from" on every money-moving form ─────────────────────────────

    [Fact]
    public async Task ManualExpense_IsPaidFromTheChosenAccount_AndWarnsWhenTheAccountIsShort()
    {
        await Post(TransactionCategory.LoanDisbursement, 40_000m, _bank.Id);
        var handler = new RecordExpenseCommandHandler(_ledger, _uow, _credit, Substitute.For<IPublisher>(), null, _money);

        var dto = await handler.Handle(new RecordExpenseCommand(TenantId, FarmId, "LaborCost", 60_000m, Day, "Wages", "", "", null, null, null,
            MoneyAccountId: _bank.Id), CancellationToken.None);

        dto.MoneyAccountId.Should().Be(_bank.Id);
        (await Balance(_bank)).Should().Be(-20_000m, "a warning never blocks the spend");
        _warnings.Messages.Should().ContainSingle(m => m.Contains("Sonali Bank has ৳40,000; this spends ৳60,000"));
    }

    [Fact]
    public async Task ManualIncome_GoesIntoTheChosenAccount()
    {
        var handler = new RecordIncomeCommandHandler(_ledger, _uow, _credit, _money);

        await handler.Handle(new RecordIncomeCommand(TenantId, FarmId, "MilkSale", 8_000m, Day, "Milk", "", "", null, null, null,
            MoneyAccountId: _bank.Id), CancellationToken.None);

        (await Balance(_bank)).Should().Be(8_000m);
        (await Balance(_cash)).Should().Be(0m);
    }

    [Fact]
    public async Task LoanReceivedAndRepaid_GoThroughTheChosenAccounts()
    {
        var loans = Substitute.For<ILoanRecordRepository>();
        LoanRecord? loan = null;
        loans.When(l => l.Add(Arg.Any<LoanRecord>())).Do(c => loan = c.Arg<LoanRecord>());
        loans.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(_ => loan);

        await new CreateLoanRecordCommandHandler(loans, _uow, _posting).Handle(new CreateLoanRecordCommand(
            TenantId, FarmId, "Krishi Bank", 100_000m, 12m, Day, "Monthly", null, _bank.Id), CancellationToken.None);
        await new RecordLoanRepaymentCommandHandler(loans, _posting, _uow, _money).Handle(new RecordLoanRepaymentCommand(
            TenantId, FarmId, loan!.Id, 11_200m, Day, "R-1", "", _cash.Id), CancellationToken.None);

        _ledger.Of(TransactionCategory.LoanDisbursement).Single().MoneyAccountId.Should().Be(_bank.Id);
        _ledger.Rows.Where(r => r.Category is TransactionCategory.LoanRepayment or TransactionCategory.InterestExpense)
            .Should().HaveCount(2).And.OnlyContain(r => r.MoneyAccountId == _cash.Id, "principal and interest both left cash in hand");
        (await Balance(_bank)).Should().Be(100_000m);
        (await Balance(_cash)).Should().Be(-11_200m);
    }

    [Fact]
    public async Task SupplierBillPayment_ComesOutOfTheChosenAccount()
    {
        var feed = await Post(TransactionCategory.FeedCost, 10_000m);
        var bill = await _credit.OpenAsync(feed, new CreditTerms("Rahim Feeds"));

        await _credit.RecordPaymentAsync(bill, 4_000m, DateOnly.FromDateTime(Day), "Bank", null, null, new PaymentSource(_bank.Id));

        _ledger.Of(TransactionCategory.SupplierPayment).Single().MoneyAccountId.Should().Be(_bank.Id);
        (await Balance(_bank)).Should().Be(-4_000m);
        (await Balance(_cash)).Should().Be(0m, "the feed itself was bought on credit");
    }

    [Fact]
    public async Task CreditPurchase_PaidNowPart_ComesOutOfTheChosenAccount()
    {
        var feed = await Post(TransactionCategory.FeedCost, 10_000m);

        await _credit.OpenAsync(feed, new CreditTerms("Rahim Feeds", PaidNowBdt: 3_000m, MoneyAccountId: _bank.Id));

        (await Balance(_bank)).Should().Be(-3_000m);
    }

    [Fact]
    public async Task FixedAssetPurchase_IsPaidFromTheChosenAccount()
    {
        var handler = new CreateFixedAssetCommandHandler(Substitute.For<IFixedAssetRepository>(), Substitute.For<IFarmAccountingSettingsRepository>(),
            _posting, _credit, _uow, null, _money);

        await handler.Handle(new CreateFixedAssetCommand(TenantId, FarmId, new SaveFixedAssetRequest(
            "Chopper", "MachineryAndEquipment", "Cash", Day, 45_000m, "StraightLine", 0m, 5m, 0m, MoneyAccountId: _bank.Id)), CancellationToken.None);

        _ledger.Of(TransactionCategory.FixedAssetPurchase).Single().MoneyAccountId.Should().Be(_bank.Id);
    }

    [Fact]
    public async Task PartnerMoney_GoesIntoAndOutOfTheChosenAccount()
    {
        var partner = Investor.Create(TenantId, FarmId, "Karim", 0m, Day);
        _investors.Add(partner);
        var handler = new RecordInvestorTransactionCommandHandler(_investors, _ledger, _uow, _money);

        await handler.Handle(new RecordInvestorTransactionCommand(TenantId, FarmId, partner.Id, "CapitalContribution", 100_000m, Day, null, null, _bank.Id), CancellationToken.None);
        await handler.Handle(new RecordInvestorTransactionCommand(TenantId, FarmId, partner.Id, "ProfitDistribution", 5_000m, Day, null, null, _cash.Id), CancellationToken.None);

        (await Balance(_bank)).Should().Be(100_000m);
        (await Balance(_cash)).Should().Be(-5_000m);
        partner.Transactions.Should().OnlyContain(t => t.FinancialTransactionId != null);
    }

    [Fact]
    public async Task ModulePostings_GoToTheAccountTheFormChose()
    {
        // Stock-in, purchase-order receipt, animal purchase / sale and purchase-return refunds are posted
        // by their module, then put through the form's chosen account.
        var poId = Guid.NewGuid();
        await _posting.PostAsync(new LedgerPosting(TenantId, FarmId, TransactionCategory.InventoryPurchase, 12_000m, Day, poId, "Inventory", "PO-1"));
        await _money.ApplyToSourceAsync(FarmId, poId, TransactionCategory.InventoryPurchase, new PaymentSource(_bank.Id));

        var stockInId = Guid.NewGuid();
        await _posting.PostAsync(new LedgerPosting(TenantId, FarmId, TransactionCategory.InventoryPurchase, 3_000m, Day, stockInId, "Inventory", "Stock in",
            MoneyAccountId: _cash.Id));

        (await _ledger.FindBySourceAsync(FarmId, poId, TransactionCategory.InventoryPurchase))!.MoneyAccountId.Should().Be(_bank.Id);
        (await _ledger.FindBySourceAsync(FarmId, stockInId, TransactionCategory.InventoryPurchase))!.MoneyAccountId.Should().Be(_cash.Id);

        // Re-posting (price corrected) keeps the chosen account.
        await _posting.PostAsync(new LedgerPosting(TenantId, FarmId, TransactionCategory.InventoryPurchase, 12_500m, Day, poId, "Inventory", "PO-1"));
        (await _ledger.FindBySourceAsync(FarmId, poId, TransactionCategory.InventoryPurchase))!.MoneyAccountId.Should().Be(_bank.Id);
    }

    [Fact]
    public async Task AnimalRegistration_CanBuyOnCredit_RaisingTheSupplierBillAtRegistration()
    {
        var (handler, livestock) = RegisterHandler();
        _ = livestock;

        var dto = await handler.Handle(Register(new CreditTermsRequest("Haat seller", PaidNowBdt: 20_000m, MoneyAccountId: _bank.Id)), CancellationToken.None);

        var purchase = _ledger.Of(TransactionCategory.AnimalPurchase).Single();
        purchase.Settlement.Should().Be(SettlementAccount.Payable, "the animal is owed to the seller");
        _bills.Bills.Should().ContainSingle(b => b.SourceDocumentId == dto.Id && b.PartyName == "Haat seller" && b.AmountBdt == 70_000m);
        _ledger.Of(TransactionCategory.SupplierPayment).Single().MoneyAccountId.Should().Be(_bank.Id, "the advance came out of the bank");
        (await Balance(_bank)).Should().Be(-20_000m);
    }

    [Fact]
    public async Task AnimalRegistration_PaidFromTheChosenAccount()
    {
        var (handler, _) = RegisterHandler();

        await handler.Handle(Register(null, _bank.Id), CancellationToken.None);

        _ledger.Of(TransactionCategory.AnimalPurchase).Single().MoneyAccountId.Should().Be(_bank.Id);
    }

    // ── Paid directly by a partner ──────────────────────────────────────────

    [Fact]
    public async Task SpendPaidDirectlyByAPartner_BooksTheirCapitalAndTheSpend_SameDay_CashNetsToZero()
    {
        var partner = Investor.Create(TenantId, FarmId, "Rahim", 0m, Day);
        _investors.Add(partner);
        var handler = new RecordExpenseCommandHandler(_ledger, _uow, _credit, Substitute.For<IPublisher>(), null, _money);

        await handler.Handle(new RecordExpenseCommand(TenantId, FarmId, "VeterinaryCost", 6_000m, Day, "Doctor", "", "", null, null, null,
            PaidByPartnerId: partner.Id), CancellationToken.None);

        var spend = _ledger.Of(TransactionCategory.VeterinaryCost).Single();
        var capital = _ledger.Of(TransactionCategory.InvestorCapital).Single();
        capital.AmountBdt.Should().Be(6_000m);
        capital.TransactionDate.Should().Be(spend.TransactionDate);
        capital.MoneyAccountId.Should().Be(_cash.Id).And.Be(spend.MoneyAccountId, "both go through the default account");
        capital.LinkedTransactionId.Should().Be(spend.Id);
        spend.LinkedTransactionId.Should().Be(capital.Id);
        (await Balance(_cash)).Should().Be(0m, "the partner's money came in and went straight out");
        AccountChart.NetProfit(_ledger.Rows).Should().Be(-6_000m, "the vet bill is still a cost");
        partner.CurrentCapitalBdt.Should().Be(6_000m);
        partner.Transactions.Single().FinancialTransactionId.Should().Be(capital.Id);
        _warnings.Messages.Should().BeEmpty("nothing was spent out of the farm's own cash");
    }

    [Fact]
    public async Task PartnerPaidModulePosting_FollowsCorrections_AndGoesWhenTheSpendGoes()
    {
        var partner = Investor.Create(TenantId, FarmId, "Rahim", 0m, Day);
        _investors.Add(partner);
        var source = Guid.NewGuid();

        await _posting.PostAsync(new LedgerPosting(TenantId, FarmId, TransactionCategory.AnimalPurchase, 70_000m, Day, source, "Livestock", "Bull",
            PaidByPartnerId: partner.Id));
        await _posting.PostAsync(new LedgerPosting(TenantId, FarmId, TransactionCategory.AnimalPurchase, 65_000m, Day.AddDays(1), source, "Livestock", "Bull"));

        var capital = _ledger.Of(TransactionCategory.InvestorCapital).Single();
        capital.AmountBdt.Should().Be(65_000m);
        capital.TransactionDate.Should().Be(Day.AddDays(1));
        partner.CurrentCapitalBdt.Should().Be(65_000m);
        partner.Transactions.Single().AmountBdt.Should().Be(65_000m);

        await _posting.ReverseAsync(FarmId, source, TransactionCategory.AnimalPurchase);
        _ledger.Of(TransactionCategory.InvestorCapital).Should().BeEmpty();
        partner.CurrentCapitalBdt.Should().Be(0m);
        partner.Transactions.Should().BeEmpty();
    }

    [Fact]
    public async Task OnlyPaymentsCanBePaidByAPartner()
    {
        var partner = Investor.Create(TenantId, FarmId, "Rahim", 0m, Day);
        _investors.Add(partner);
        var sale = await Post(TransactionCategory.MilkSale, 1_000m);

        var act = () => _money.ApplyAsync(sale, new PaymentSource(PartnerId: partner.Id));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── Capital, share and the profit split ─────────────────────────────────

    [Fact]
    public void ProfitSplit_IsTimeWeightedByCapitalDays()
    {
        // The worked example: A puts in 1,00,000 for the whole year; B puts in 1,00,000 half-way through.
        var a = Investor.Create(TenantId, FarmId, "A", 100_000m, new DateTime(2026, 1, 1));
        var b = Investor.Create(TenantId, FarmId, "B", 100_000m, new DateTime(2026, 7, 2));

        var shares = PartnerCapital.TimeWeightedShares([a, b], new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        shares[a.Id].Should().BeApproximately(0.667m, 0.002m, "A held 1,00,000 for 365 days");
        shares[b.Id].Should().BeApproximately(0.333m, 0.002m, "B held the same 1,00,000 for 183 days");
        shares.Values.Sum().Should().Be(1m);

        var parts = PartnerCapital.Split(90_000m, shares);
        parts.Values.Sum().Should().Be(90_000m, "rounding never loses or invents a taka");
        parts[a.Id].Should().BeApproximately(59_945m, 50m);
    }

    [Fact]
    public void CapitalDays_CountWithdrawalsFromTheirDay()
    {
        var a = Investor.Create(TenantId, FarmId, "A", 100_000m, new DateTime(2026, 1, 1));
        a.RecordTransaction(TenantId, InvestorTransactionType.CapitalWithdrawal, 50_000m, new DateTime(2026, 1, 11));
        a.RecordTransaction(TenantId, InvestorTransactionType.ProfitDistribution, 9_999m, new DateTime(2026, 1, 5));

        // 10 days at 1,00,000 then 10 days at 50,000; profit paid out does not reduce capital.
        PartnerCapital.CapitalDays(a.Transactions, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 20)).Should().Be(1_500_000m);
    }

    [Fact]
    public async Task PartnersOverview_SharesAreCapitalBased_AndAnOldAgreedPercentageIsIgnored()
    {
        _investors.Add(Investor.Create(TenantId, FarmId, "Owner", 30_000m, Day, agreedProfitSharePercentage: 80m));
        _investors.Add(Investor.Create(TenantId, FarmId, "Partner", 70_000m, Day));
        var handler = new GetPartnersOverviewQueryHandler(new FinancialStatementEngine(_ledger), _investors, _accounts, new FixedClock(Day));

        var overview = await handler.Handle(new GetPartnersOverviewQuery(FarmId), CancellationToken.None);

        overview.TotalCapitalBdt.Should().Be(100_000m);
        overview.Partners.Single(p => p.Name == "Owner").SharePercent.Should().Be(30m);
        overview.Partners.Single(p => p.Name == "Partner").SharePercent.Should().Be(70m);
    }

    [Fact]
    public async Task ProfitAllocationQuery_SplitsTheDistributableProfitByCapitalDays()
    {
        _investors.Add(Investor.Create(TenantId, FarmId, "A", 100_000m, new DateTime(2026, 1, 1)));
        _investors.Add(Investor.Create(TenantId, FarmId, "B", 100_000m, new DateTime(2026, 7, 2)));
        await Post(TransactionCategory.MilkSale, 200_000m, date: new DateTime(2026, 3, 1));
        await Post(TransactionCategory.LaborCost, 100_000m, date: new DateTime(2026, 4, 1));
        var handler = new GetPartnerProfitAllocationQueryHandler(new FinancialStatementEngine(_ledger), _investors, _accounts, new FixedClock(new DateTime(2026, 12, 31)));

        var allocation = await handler.Handle(new GetPartnerProfitAllocationQuery(FarmId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)), CancellationToken.None);

        allocation.NetProfitBdt.Should().Be(100_000m);
        allocation.DistributableProfitBdt.Should().Be(95_000m, "5% is set aside for charity first");
        allocation.Lines.Single(l => l.Name == "A").SharePercent.Should().BeApproximately(66.6m, 0.2m);
        allocation.Lines.Single(l => l.Name == "B").SharePercent.Should().BeApproximately(33.4m, 0.2m);
        allocation.Lines.Sum(l => l.AllocatedBdt).Should().Be(95_000m);
    }

    [Fact]
    public async Task WhoseMoney_SplitsASpendByCapitalOnTheDayOfTheSpend()
    {
        _investors.Add(Investor.Create(TenantId, FarmId, "A", 75_000m, Day));
        _investors.Add(Investor.Create(TenantId, FarmId, "B", 25_000m, Day));
        var animalId = Guid.NewGuid();
        var purchase = FinancialTransaction.Create(TenantId, FarmId, TransactionType.Expense, TransactionCategory.AnimalPurchase, 80_000m, Day, animalId: animalId);
        await _ledger.AddAsync(purchase);
        await Post(TransactionCategory.FeedCost, 500m, settlement: SettlementAccount.Inventory); // stock used is not new spending
        var handler = new GetWhoseMoneyQueryHandler(new FinancialStatementEngine(_ledger), _investors, new FixedClock(Day));

        var result = await handler.Handle(new GetWhoseMoneyQuery(FarmId, AnimalId: animalId), CancellationToken.None);

        var row = result.Rows.Single();
        row.Parts.Single(p => p.Name == "A").AmountBdt.Should().Be(60_000m);
        row.Parts.Single(p => p.Name == "B").AmountBdt.Should().Be(20_000m);
    }

    [Fact]
    public void ShortOfMoneyWarning_IsTranslated()
    {
        var english = MoneyAccountService.ShortMessage("Cash in hand", 40_000m, 60_000m);

        MessageTranslator.Default.Translate(english, "bn").Should().Be(
            "হাতে নগদ-এ আছে ৳40,000; এখানে খরচ হচ্ছে ৳60,000। অংশীদারের টাকা যোগ করুন বা খাতার মধ্যে স্থানান্তর করুন।");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private (RegisterAnimalCommandHandler Handler, LivestockAccountingService Livestock) RegisterHandler()
    {
        var repo = Substitute.For<IAnimalRepository>();
        Animal? added = null;
        repo.When(r => r.Add(Arg.Any<Animal>())).Do(c => added = c.Arg<Animal>());
        var tenants = Substitute.For<ITenantRepository>();
        tenants.GetByIdAsync(TenantId, Arg.Any<CancellationToken>()).Returns(Tenant.Create("Test Farm", "test-farm", SubscriptionTier.Standard));
        tenants.GetTenantUsageCountsAsync(TenantId, Arg.Any<CancellationToken>()).Returns((1, 1, 0));
        var tenantService = Substitute.For<ITenantService>();
        tenantService.TenantId.Returns(TenantId);
        var user = Substitute.For<ICurrentUserService>();
        user.UserId.Returns(Guid.NewGuid());
        var livestock = new LivestockAccountingService(_posting, _ledger);
        // Stands in for Finance's AnimalRegisteredEvent handler: books the purchase as a cash purchase.
        var publisher = new PurchasePostingPublisher(() => added, livestock);
        return (new RegisterAnimalCommandHandler(repo, tenants, _uow, tenantService, user, publisher, null, _credit, _money), livestock);
    }

    private static RegisterAnimalCommand Register(CreditTermsRequest? credit, Guid? account = null) => new(
        FarmId, "B-07", TagType.EarTag, AnimalSpecies.CattleBeef, Guid.NewGuid(), AnimalSex.Male,
        new DateOnly(2024, 1, 1), AcquisitionType.Purchased, new DateOnly(2026, 8, 20), 70_000m, null, null, null,
        SkipHealthProgramme: true, MoneyAccountId: account, Credit: credit);

    private sealed class PurchasePostingPublisher(Func<Animal?> animal, LivestockAccountingService livestock) : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Book(cancellationToken);

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Book(cancellationToken);

        private async Task Book(CancellationToken cancellationToken)
        {
            if (animal() is { } a)
                await livestock.SyncPurchaseAsync(a, cancellationToken);
        }
    }

    private sealed class FixedClock(DateTime today) : IFarmClock
    {
        public DateTime UtcNow => today;
        public TimeZoneInfo TimeZone => TimeZoneInfo.Utc;
        public DateOnly Today => DateOnly.FromDateTime(today);
        public DateTime Now => today;
        public DateOnly TodayFor(Guid tenantId) => Today;
        public DateOnly TodayForFarm(Guid tenantId, Guid farmId) => Today;
        public TimeZoneInfo ZoneFor(Guid tenantId, Guid? farmId = null) => TimeZoneInfo.Utc;
    }
}

/// <summary>A list-backed money-account register.</summary>
internal sealed class InMemoryMoneyAccountRepository : IMoneyAccountRepository
{
    public List<MoneyAccount> Accounts { get; } = [];

    public Task<MoneyAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Accounts.FirstOrDefault(a => a.Id == id));

    public Task<IReadOnlyList<MoneyAccount>> GetByFarmIdAsync(Guid farmId, bool includeInactive = true, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MoneyAccount>>(Accounts.Where(a => a.FarmId == farmId && (includeInactive || a.IsActive)).ToList());

    public Task<MoneyAccount?> GetDefaultAsync(Guid farmId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Accounts.FirstOrDefault(a => a.FarmId == farmId && a.IsDefault));

    public void Add(MoneyAccount account) => Accounts.Add(account);

    public void Update(MoneyAccount account) { }
}

/// <summary>A list-backed partner (investor) register.</summary>
internal sealed class InMemoryInvestorRepository : IInvestorRepository
{
    public List<Investor> Investors { get; } = [];

    public Task<Investor?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Investors.FirstOrDefault(i => i.Id == id));

    public Task<Investor?> GetByIdWithTransactionsAsync(Guid id, CancellationToken cancellationToken = default) => GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<Investor>> GetByFarmIdAsync(Guid farmId, bool includeTransactions = false, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Investor>>(Investors.Where(i => i.FarmId == farmId).ToList());

    public void Add(Investor investor) => Investors.Add(investor);

    public void Update(Investor investor) { }

    public void Delete(Investor investor) => Investors.Remove(investor);
}
