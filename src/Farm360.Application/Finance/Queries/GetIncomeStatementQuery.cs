using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Services;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance.Accounting;
using MediatR;

namespace Farm360.Application.Finance.Queries;

/// <param name="FromDate">Defaults to the start of the financial year containing <paramref name="ToDate"/>.</param>
/// <param name="ToDate">Defaults to today.</param>
/// <param name="IncludeComparative">Adds the same period one year earlier.</param>
public record GetIncomeStatementQuery(
    Guid FarmId,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    bool IncludeComparative = true
) : IRequest<IncomeStatementDto>;

/// <summary>
/// Statement of profit or loss for a period: revenue, cost of sales (the animals sold and the
/// feed, health, labour and consumables that raised them), gross profit, operating expenses
/// (overheads, depreciation, disposals), finance costs and net profit.
/// </summary>
/// <remarks>
/// Built from the same <see cref="IFinancialStatementEngine"/> lines as the balance sheet, so the
/// net profit for the financial year to date equals the balance sheet's "Profit for the year".
/// </remarks>
public class GetIncomeStatementQueryHandler : IRequestHandler<GetIncomeStatementQuery, IncomeStatementDto>
{
    /// <summary>Direct costs of the livestock sold: the animals and what it took to raise them.</summary>
    public static readonly IReadOnlySet<string> CostOfSalesAccounts = new HashSet<string>(StringComparer.Ordinal)
    {
        "5000", "5005", "5020", "5030", "5040", "5090", "5100",
    };

    public static readonly IReadOnlySet<string> FinanceCostAccounts = new HashSet<string>(StringComparer.Ordinal)
    {
        "5300",
    };

    private readonly IFinancialStatementEngine _engine;

    public GetIncomeStatementQueryHandler(IFinancialStatementEngine engine)
    {
        _engine = engine;
    }

    public async Task<IncomeStatementDto> Handle(GetIncomeStatementQuery request, CancellationToken cancellationToken)
    {
        var to = DateOnly.FromDateTime(request.ToDate ?? DateTime.UtcNow);
        var ledger = await _engine.LoadAsync(request.FarmId, to, cancellationToken);
        var from = request.FromDate is { } f
            ? DateOnly.FromDateTime(f)
            : FiscalYear.StartOf(to, ledger.FiscalYearStartMonth);
        if (from > to)
            throw new ArgumentException("The period start must be on or before its end.");

        var current = ledger.MovementsBetween(from, to);
        Dictionary<string, decimal>? prior = null;
        DateOnly? priorFrom = null, priorTo = null;
        if (request.IncludeComparative)
        {
            priorFrom = from.AddYears(-1);
            priorTo = to.AddYears(-1);
            prior = ledger.MovementsBetween(priorFrom.Value, priorTo.Value);
        }

        var sections = Build(current, prior);
        return new IncomeStatementDto(
            request.FarmId,
            from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            to.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            priorFrom?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            priorTo?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            sections,
            Totals(sections, s => s.TotalBdt),
            prior is null ? null : Totals(sections, s => s.PriorTotalBdt ?? 0m),
            ledger.FiscalYearStartMonth,
            ledger.BooksStartDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
    }

    public static string SectionOf(string accountCode)
    {
        var account = AccountChart.Account(accountCode);
        if (account.Class == AccountClass.Revenue) return "revenue";
        if (CostOfSalesAccounts.Contains(accountCode)) return "cost-of-sales";
        if (FinanceCostAccounts.Contains(accountCode)) return "finance-costs";
        return "operating-expenses";
    }

    private static List<IncomeStatementSectionDto> Build(
        Dictionary<string, decimal> current,
        Dictionary<string, decimal>? prior)
    {
        var codes = current.Keys
            .Concat(prior?.Keys ?? Enumerable.Empty<string>())
            .Where(FarmLedger.IsProfitAndLoss)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

        var definitions = new[]
        {
            ("revenue", "Revenue", "আয়"),
            ("cost-of-sales", "Cost of Sales", "বিক্রীত পণ্যের ব্যয়"),
            ("operating-expenses", "Operating Expenses", "পরিচালন ব্যয়"),
            ("finance-costs", "Finance Costs", "আর্থিক ব্যয়"),
        };

        var sections = new List<IncomeStatementSectionDto>();
        foreach (var (key, name, nameBn) in definitions)
        {
            var lines = new List<IncomeStatementLineDto>();
            foreach (var code in codes.Where(c => SectionOf(c) == key))
            {
                // Revenue is a credit movement, expenses a debit; both shown as positive amounts.
                var sign = key == "revenue" ? -1m : 1m;
                var amount = R(sign * current.GetValueOrDefault(code));
                decimal? priorAmount = prior is null ? null : R(sign * prior.GetValueOrDefault(code));
                if (amount == 0m && (priorAmount ?? 0m) == 0m)
                    continue;

                var account = AccountChart.Account(code);
                lines.Add(new IncomeStatementLineDto(code, account.Name, account.NameBn, amount, priorAmount));
            }

            sections.Add(new IncomeStatementSectionDto(
                key, name, nameBn, lines,
                R(lines.Sum(l => l.AmountBdt)),
                prior is null ? null : R(lines.Sum(l => l.PriorAmountBdt ?? 0m))));
        }

        return sections;
    }

    private static IncomeStatementTotalsDto Totals(
        IReadOnlyList<IncomeStatementSectionDto> sections,
        Func<IncomeStatementSectionDto, decimal> total)
    {
        decimal Of(string key) => total(sections.Single(s => s.Key == key));
        var revenue = Of("revenue");
        var costOfSales = Of("cost-of-sales");
        var gross = revenue - costOfSales;
        var operating = Of("operating-expenses");
        var operatingProfit = gross - operating;
        var finance = Of("finance-costs");
        return new IncomeStatementTotalsDto(
            R(revenue), R(costOfSales), R(gross), R(operating), R(operatingProfit), R(finance), R(operatingProfit - finance));
    }

    private static decimal R(decimal value) => Math.Round(value, 2);
}
