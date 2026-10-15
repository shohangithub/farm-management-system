using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Services;
using Farm360.Contracts.Finance;
using MediatR;

namespace Farm360.Application.Finance.Queries;

public record GetTrialBalanceQuery(
    Guid FarmId,
    DateTime? AsOfDate = null
) : IRequest<TrialBalanceDto>;

/// <summary>
/// Trial balance from the double-entry journal: every account's net balance up to the date, on its
/// debit or credit side. Assets and expenses are debit balances; liabilities, equity and revenue
/// are credit balances.
/// </summary>
/// <remarks>
/// Read from <see cref="IFinancialStatementEngine"/>, so opening balances, adjustments and
/// depreciation are included. Opening balances are one-sided: if they are incomplete, debits and
/// credits differ by exactly the balance sheet's Difference.
/// </remarks>
public class GetTrialBalanceQueryHandler : IRequestHandler<GetTrialBalanceQuery, TrialBalanceDto>
{
    private readonly IFinancialStatementEngine _engine;

    public GetTrialBalanceQueryHandler(IFinancialStatementEngine engine)
    {
        _engine = engine;
    }

    public async Task<TrialBalanceDto> Handle(GetTrialBalanceQuery request, CancellationToken cancellationToken)
    {
        var asOf = request.AsOfDate ?? DateTime.UtcNow;
        var day = DateOnly.FromDateTime(asOf);

        var ledger = await _engine.LoadAsync(request.FarmId, day, cancellationToken);
        var balances = ledger.BalancesAt(day);

        var lines = new List<TrialBalanceLineDto>();
        foreach (var (code, balance) in balances.OrderBy(b => b.Key, StringComparer.Ordinal))
        {
            var amount = Math.Round(balance, 2);
            if (amount == 0m)
                continue;

            var account = AccountChart.Account(code);
            lines.Add(new TrialBalanceLineDto(
                code,
                account.Name,
                account.Class.ToString(),
                amount > 0 ? amount : 0m,
                amount < 0 ? -amount : 0m));
        }

        var totalDebit = lines.Sum(l => l.DebitBdt);
        var totalCredit = lines.Sum(l => l.CreditBdt);
        var difference = Math.Round(Math.Abs(totalDebit - totalCredit), 2);

        return new TrialBalanceDto(
            request.FarmId,
            asOf,
            lines,
            Math.Round(totalDebit, 2),
            Math.Round(totalCredit, 2),
            difference == 0m,
            difference);
    }
}
