using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Services;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Interfaces;
using MediatR;

namespace Farm360.Application.Finance.Queries;

/// <summary>Every partner's capital, live share %, profit allocated / paid / due, and the farm's net assets.</summary>
public sealed record GetPartnersOverviewQuery(Guid FarmId, bool IncludeInactive = true) : IRequest<PartnersOverviewDto>;

/// <summary>How a period's profit is split between partners by capital-days. Default period: this fiscal year to date.</summary>
public sealed record GetPartnerProfitAllocationQuery(Guid FarmId, DateOnly? FromDate = null, DateOnly? ToDate = null)
    : IRequest<PartnerProfitAllocationDto>;

/// <summary>One partner's statement: capital movements, profit allocated, and their part of the farm's net assets.</summary>
public sealed record GetPartnerStatementQuery(Guid FarmId, Guid PartnerId, DateOnly? FromDate = null, DateOnly? ToDate = null)
    : IRequest<PartnerStatementDto>;

/// <summary>
/// "Whose money where": spends (animal purchases, stock, assets, expenses) with each partner's
/// pro-rata part, by capital on the day of the spend. Filter by animal for one animal's costs.
/// </summary>
public sealed record GetWhoseMoneyQuery(Guid FarmId, DateOnly? FromDate = null, DateOnly? ToDate = null, Guid? AnimalId = null, Guid? TransactionId = null)
    : IRequest<WhoseMoneyDto>;

/// <summary>The partnership arithmetic shared by the partner queries and reports.</summary>
public sealed class PartnerBook
{
    private readonly Dictionary<Guid, string> _accountNames;
    private readonly Guid _defaultAccountId;

    private PartnerBook(FarmLedger ledger, IReadOnlyList<Investor> partners, IReadOnlyList<MoneyAccount> accounts, DateOnly today)
    {
        Ledger = ledger;
        Partners = partners;
        Today = today;
        _accountNames = accounts.ToDictionary(a => a.Id, a => a.Name);
        _defaultAccountId = accounts.FirstOrDefault(a => a.IsDefault)?.Id ?? Guid.Empty;
        FirstDay = partners.SelectMany(p => p.Transactions).Select(t => (DateOnly?)DateOnly.FromDateTime(t.TransactionDate)).Min();
        Allocated = AllocateLifetime();
    }

    public FarmLedger Ledger { get; }
    public IReadOnlyList<Investor> Partners { get; }
    public DateOnly Today { get; }

    /// <summary>The first day any partner money moved (null when no partner has any transaction).</summary>
    public DateOnly? FirstDay { get; }

    /// <summary>Profit allocated to each partner since the first partner money, fiscal year by fiscal year.</summary>
    public IReadOnlyDictionary<Guid, decimal> Allocated { get; }

    public static async Task<PartnerBook> LoadAsync(
        IFinancialStatementEngine engine, IInvestorRepository investors, IMoneyAccountRepository accounts,
        Guid farmId, DateOnly today, CancellationToken cancellationToken)
    {
        var ledger = await engine.LoadAsync(farmId, today, cancellationToken);
        var partners = await investors.GetByFarmIdAsync(farmId, includeTransactions: true, cancellationToken);
        var moneyAccounts = await accounts.GetByFarmIdAsync(farmId, includeInactive: true, cancellationToken);
        return new PartnerBook(ledger, partners, moneyAccounts, today);
    }

    public decimal TotalCapital => Partners.Sum(p => Math.Max(0m, PartnerCapital.CapitalAt(p.Transactions, Today)));

    public PartnerSummaryDto Summary(Investor p)
    {
        ArgumentNullException.ThrowIfNull(p);
        var deposited = p.Transactions.Where(t => t.Type == InvestorTransactionType.CapitalContribution).Sum(t => t.AmountBdt);
        var withdrawn = p.Transactions.Where(t => t.Type == InvestorTransactionType.CapitalWithdrawal).Sum(t => t.AmountBdt);
        var paid = p.Transactions.Where(t => t.Type == InvestorTransactionType.ProfitDistribution).Sum(t => t.AmountBdt);
        var capital = Math.Max(0m, deposited - withdrawn);
        var total = TotalCapital;
        var allocated = Allocated.GetValueOrDefault(p.Id);
        return new PartnerSummaryDto(
            p.Id, p.Name, p.Phone, p.IsActive, p.InvestmentDate,
            Math.Round(deposited, 2), Math.Round(withdrawn, 2), Math.Round(capital, 2),
            total > 0 ? Math.Round(capital / total * 100m, 2) : 0m,
            Math.Round(allocated, 2), Math.Round(paid, 2), Math.Round(allocated - paid, 2),
            Math.Round(capital + allocated - paid, 2));
    }

    public PartnerProfitAllocationDto Allocate(DateOnly from, DateOnly to)
    {
        var net = Ledger.ProfitBetween(from, to);
        var (charity, distributable) = PartnerCapital.Distributable(net);
        var days = to.DayNumber - from.DayNumber + 1;
        var capitalDays = Partners.ToDictionary(p => p.Id, p => PartnerCapital.CapitalDays(p.Transactions, from, to));
        var shares = PartnerCapital.TimeWeightedShares(Partners, from, to);
        var parts = PartnerCapital.Split(Math.Round(distributable, 2), shares);

        var lines = Partners
            .Where(p => capitalDays[p.Id] > 0)
            .Select(p => new PartnerAllocationLineDto(
                p.Id, p.Name, Math.Round(capitalDays[p.Id], 0),
                days > 0 ? Math.Round(capitalDays[p.Id] / days, 2) : 0m,
                Math.Round(shares.GetValueOrDefault(p.Id) * 100m, 2),
                parts.GetValueOrDefault(p.Id)))
            .OrderByDescending(l => l.SharePercent)
            .ToList();

        return new PartnerProfitAllocationDto(from, to, days, Math.Round(net, 2), PartnerCapital.CharityPercentage,
            charity, Math.Round(distributable, 2), lines);
    }

    /// <summary>The farm's net assets now (what it owns less what it owes), line by line.</summary>
    public IReadOnlyList<(string Key, string Name, string NameBn, decimal Amount)> NetAssets()
    {
        var b = Ledger.BalancesAt(Today);
        decimal Bal(params string[] codes) => codes.Sum(c => b.GetValueOrDefault(c));
        return
        [
            ("animals", "Animals (at cost)", "পশু (ক্রয়মূল্যে)", Bal(AccountChart.LivestockAccount, AccountChart.LivestockCarriedCostAccount)),
            ("stock", "Feed, medicine & supplies in stock", "মজুদ খাদ্য, ঔষধ ও মালামাল", Bal(AccountChart.InventoryAccount)),
            ("cash", "Cash & bank", "নগদ ও ব্যাংক", Bal(AccountChart.CashAccount)),
            ("receivables", "Money owed to the farm", "খামারের পাওনা", Bal(AccountChart.ReceivableAccount, AccountChart.AdvancesDepositsAccount)),
            ("fixed", "Sheds, machinery & other fixed assets", "শেড, যন্ত্রপাতি ও অন্যান্য স্থায়ী সম্পদ", Bal(AccountChart.FixedAssetAccount, AccountChart.AccumulatedDepreciationAccount)),
            ("loans", "Loans owed", "প্রদেয় ঋণ", Bal(AccountChart.LoanPayableAccount)),
            ("payables", "Owed to suppliers & others", "সরবরাহকারী ও অন্যান্য দেনা", Bal(AccountChart.PayableAccount, AccountChart.ProvisionsAccount, AccountChart.OtherLiabilitiesAccount)),
        ];
    }

    /// <summary>Name of the money account a ledger posting went through (null for non-cash or unknown).</summary>
    public string? AccountName(Guid? financialTransactionId)
    {
        if (financialTransactionId is not { } id)
            return null;
        var posting = Ledger.AllPostings.FirstOrDefault(t => t.Id == id);
        return posting is null || !posting.MovesMoneyAccount
            ? null
            : _accountNames.GetValueOrDefault(MoneyAccountBalances.AccountOf(posting, _defaultAccountId));
    }

    private Dictionary<Guid, decimal> AllocateLifetime()
    {
        var totals = Partners.ToDictionary(p => p.Id, _ => 0m);
        if (FirstDay is not { } first || first > Today)
            return totals;

        foreach (var (from, to) in PartnerCapital.FiscalPeriods(first, Today, Ledger.FiscalYearStartMonth))
        {
            var (_, distributable) = PartnerCapital.Distributable(Ledger.ProfitBetween(from, to));
            var parts = PartnerCapital.Split(Math.Round(distributable, 2), PartnerCapital.TimeWeightedShares(Partners, from, to));
            foreach (var (id, amount) in parts)
                totals[id] += amount;
        }

        return totals;
    }
}

public sealed class GetPartnersOverviewQueryHandler(
    IFinancialStatementEngine engine,
    IInvestorRepository investors,
    IMoneyAccountRepository accounts,
    IFarmClock? clock = null) : IRequestHandler<GetPartnersOverviewQuery, PartnersOverviewDto>
{
    private readonly IFarmClock _clock = clock ?? Farm360.Application.Common.Time.FarmClock.System;

    public async Task<PartnersOverviewDto> Handle(GetPartnersOverviewQuery request, CancellationToken cancellationToken)
    {
        var book = await PartnerBook.LoadAsync(engine, investors, accounts, request.FarmId, _clock.Today, cancellationToken);
        var partners = book.Partners
            .Where(p => request.IncludeInactive || p.IsActive)
            .Select(book.Summary)
            .OrderByDescending(p => p.CapitalBdt)
            .ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return new PartnersOverviewDto(
            Math.Round(book.TotalCapital, 2),
            Math.Round(book.NetAssets().Sum(l => l.Amount), 2),
            partners.Sum(p => p.ProfitAllocatedBdt),
            partners.Sum(p => p.ProfitPaidBdt),
            partners);
    }
}

public sealed class GetPartnerProfitAllocationQueryHandler(
    IFinancialStatementEngine engine,
    IInvestorRepository investors,
    IMoneyAccountRepository accounts,
    IFarmClock? clock = null) : IRequestHandler<GetPartnerProfitAllocationQuery, PartnerProfitAllocationDto>
{
    private readonly IFarmClock _clock = clock ?? Farm360.Application.Common.Time.FarmClock.System;

    public async Task<PartnerProfitAllocationDto> Handle(GetPartnerProfitAllocationQuery request, CancellationToken cancellationToken)
    {
        var to = request.ToDate ?? _clock.Today;
        var book = await PartnerBook.LoadAsync(engine, investors, accounts, request.FarmId, to, cancellationToken);
        var from = request.FromDate ?? FiscalYearStart(to, book.Ledger.FiscalYearStartMonth);
        if (to < from)
            (from, to) = (to, from);
        return book.Allocate(from, to);
    }

    internal static DateOnly FiscalYearStart(DateOnly day, int startMonth)
    {
        var month = startMonth is >= 1 and <= 12 ? startMonth : 1;
        var year = day.Month >= month ? day.Year : day.Year - 1;
        return new DateOnly(year, month, 1);
    }
}

public sealed class GetPartnerStatementQueryHandler(
    IFinancialStatementEngine engine,
    IInvestorRepository investors,
    IMoneyAccountRepository accounts,
    IFarmClock? clock = null) : IRequestHandler<GetPartnerStatementQuery, PartnerStatementDto>
{
    private readonly IFarmClock _clock = clock ?? Farm360.Application.Common.Time.FarmClock.System;

    public async Task<PartnerStatementDto> Handle(GetPartnerStatementQuery request, CancellationToken cancellationToken)
    {
        var today = _clock.Today;
        var book = await PartnerBook.LoadAsync(engine, investors, accounts, request.FarmId, today, cancellationToken);
        var partner = book.Partners.FirstOrDefault(p => p.Id == request.PartnerId)
            ?? throw new NotFoundException(nameof(Investor), request.PartnerId);

        var to = request.ToDate ?? today;
        var from = request.FromDate
            ?? (book.FirstDay is { } first && first < to ? first : GetPartnerProfitAllocationQueryHandler.FiscalYearStart(to, book.Ledger.FiscalYearStartMonth));
        if (to < from)
            (from, to) = (to, from);

        var opening = PartnerCapital.CapitalAt(partner.Transactions, from.AddDays(-1));
        var capital = opening;
        var lines = new List<PartnerLedgerLineDto>();
        foreach (var t in partner.Transactions
                     .Where(t => DateOnly.FromDateTime(t.TransactionDate) >= from && DateOnly.FromDateTime(t.TransactionDate) <= to)
                     .OrderBy(t => t.TransactionDate).ThenBy(t => t.CreatedAtUtc))
        {
            capital += PartnerCapital.CapitalEffect(t);
            lines.Add(new PartnerLedgerLineDto(t.Id, t.TransactionDate, t.Type.ToString(), t.AmountBdt, Math.Round(capital, 2),
                book.AccountName(t.FinancialTransactionId), t.ReferenceId, t.Notes));
        }

        var allocation = book.Allocate(from, to);
        var allocated = allocation.Lines.FirstOrDefault(l => l.PartnerId == partner.Id)?.AllocatedBdt ?? 0m;
        if (allocated != 0m)
        {
            // Dated the period end, but never in the future: "this calendar year" ends on 31 Dec,
            // while the profit is only allocated up to today (farm time).
            var allocatedOn = to < today ? to : today;
            if (allocatedOn < from)
                allocatedOn = from;
            lines.Add(new PartnerLedgerLineDto(null, allocatedOn.ToDateTime(TimeOnly.MinValue), "ProfitAllocation", allocated, Math.Round(capital, 2),
                null, null, $"Share of profit {from:dd MMM yyyy} – {to:dd MMM yyyy} by capital-days"));
        }

        var summary = book.Summary(partner);
        var share = summary.SharePercent / 100m;
        var netAssets = book.NetAssets()
            .Select(l => new NetAssetLineDto(l.Key, l.Name, l.NameBn, Math.Round(l.Amount, 2), Math.Round(l.Amount * share, 2)))
            .ToList();
        var farmNet = netAssets.Sum(l => l.FarmBdt);

        return new PartnerStatementDto(summary, from, to, Math.Round(opening, 2), Math.Round(capital, 2), lines, allocation,
            netAssets, farmNet, Math.Round(farmNet * share, 2));
    }
}

public sealed class GetWhoseMoneyQueryHandler(
    IFinancialStatementEngine engine,
    IInvestorRepository investors,
    IFarmClock? clock = null) : IRequestHandler<GetWhoseMoneyQuery, WhoseMoneyDto>
{
    private const int MaxRows = 300;
    private readonly IFarmClock _clock = clock ?? Farm360.Application.Common.Time.FarmClock.System;

    public async Task<WhoseMoneyDto> Handle(GetWhoseMoneyQuery request, CancellationToken cancellationToken)
    {
        var to = request.ToDate ?? _clock.Today;
        var ledger = await engine.LoadAsync(request.FarmId, to, cancellationToken);
        var partners = await investors.GetByFarmIdAsync(request.FarmId, includeTransactions: true, cancellationToken);
        var names = partners.ToDictionary(p => p.Id, p => p.Name);

        var from = request.FromDate ?? (request.AnimalId is null && request.TransactionId is null ? to.AddMonths(-3) : DateOnly.MinValue);
        var spends = ledger.AllPostings
            .Where(IsSpend)
            .Where(t => request.TransactionId is null || t.Id == request.TransactionId)
            .Where(t => request.AnimalId is null || t.AnimalId == request.AnimalId)
            .Where(t => DateOnly.FromDateTime(t.TransactionDate) >= from && DateOnly.FromDateTime(t.TransactionDate) <= to)
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.CreatedAtUtc)
            .Take(MaxRows)
            .ToList();

        var sharesByDay = new Dictionary<DateOnly, Dictionary<Guid, decimal>>();
        var rows = new List<WhoseMoneyRowDto>();
        foreach (var t in spends)
        {
            var day = DateOnly.FromDateTime(t.TransactionDate);
            if (!sharesByDay.TryGetValue(day, out var shares))
                sharesByDay[day] = shares = PartnerCapital.SharesAt(partners, day);

            var parts = PartnerCapital.Split(t.AmountBdt, shares)
                .Select(p => new PartnerPartDto(p.Key, names[p.Key], Math.Round(shares[p.Key] * 100m, 2), p.Value))
                .OrderByDescending(p => p.AmountBdt)
                .ToList();
            var head = AccountChart.For(t.Category);
            rows.Add(new WhoseMoneyRowDto(t.Id, t.TransactionDate, AccountChart.Describe(t), t.Category.ToString(),
                head.Name, head.NameBn, t.AmountBdt, t.AnimalId, parts));
        }

        var totals = rows.SelectMany(r => r.Parts)
            .GroupBy(p => p.PartnerId)
            .Select(g => new PartnerPartDto(g.Key, names[g.Key], 0m, g.Sum(p => p.AmountBdt)))
            .ToList();
        var total = rows.Sum(r => r.AmountBdt);
        totals = totals.Select(p => p with { SharePercent = total > 0 ? Math.Round(p.AmountBdt / total * 100m, 2) : 0m })
            .OrderByDescending(p => p.AmountBdt)
            .ToList();

        return new WhoseMoneyDto(
            partners.Select(p => new PartnerRefDto(p.Id, p.Name)).ToList(),
            rows, total, totals);
    }

    /// <summary>
    /// Money the farm actually spent or committed: purchases of animals, stock and assets, and
    /// expenses paid or owed. Stock used from the store, cost of animals sold and transfers are
    /// not new spending (the money left when the stock or animal was bought).
    /// </summary>
    internal static bool IsSpend(FinancialTransaction t) =>
        t.Type == TransactionType.Expense
        && t.Settlement is SettlementAccount.Cash or SettlementAccount.Payable
        && !t.IsAccountTransfer
        && AccountChart.For(t.Category).Class is AccountClass.Expense or AccountClass.Asset;
}
