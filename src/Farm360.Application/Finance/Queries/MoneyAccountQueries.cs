using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Commands;
using Farm360.Application.Finance.Services;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Interfaces;
using MediatR;

namespace Farm360.Application.Finance.Queries;

/// <summary>The farm's money accounts with their balances (creates the default account if the farm has none yet).</summary>
public sealed record GetMoneyAccountsQuery(Guid TenantId, Guid FarmId, bool IncludeInactive = true) : IRequest<IReadOnlyList<MoneyAccountDto>>;

/// <summary>
/// An account's cash book for a period: opening balance, every receipt and payment with a running
/// balance, closing balance. <see cref="AccountId"/> null = the default account.
/// </summary>
public sealed record GetMoneyAccountStatementQuery(Guid FarmId, Guid? AccountId, DateOnly? FromDate = null, DateOnly? ToDate = null)
    : IRequest<MoneyAccountStatementDto>;

public sealed class GetMoneyAccountsQueryHandler(
    IMoneyAccountRepository accounts,
    IMoneyAccountService money,
    IUnitOfWork unitOfWork) : IRequestHandler<GetMoneyAccountsQuery, IReadOnlyList<MoneyAccountDto>>
{
    public async Task<IReadOnlyList<MoneyAccountDto>> Handle(GetMoneyAccountsQuery request, CancellationToken cancellationToken)
    {
        if (await accounts.GetDefaultAsync(request.FarmId, cancellationToken) is null)
        {
            await money.GetDefaultAsync(request.TenantId, request.FarmId, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var balances = await money.GetBalancesAsync(request.FarmId, cancellationToken: cancellationToken);
        return balances
            .Where(b => request.IncludeInactive || b.Account.IsActive)
            .OrderByDescending(b => b.Account.IsDefault)
            .ThenByDescending(b => b.Account.IsActive)
            .ThenBy(b => b.Account.Kind)
            .ThenBy(b => b.Account.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(b => b.Account.ToDto(b.BalanceBdt))
            .ToList();
    }
}

public sealed class GetMoneyAccountStatementQueryHandler(
    IMoneyAccountRepository accounts,
    IFinancialStatementEngine engine,
    IFarmClock? clock = null) : IRequestHandler<GetMoneyAccountStatementQuery, MoneyAccountStatementDto>
{
    private readonly IFarmClock _clock = clock ?? Farm360.Application.Common.Time.FarmClock.System;

    public async Task<MoneyAccountStatementDto> Handle(GetMoneyAccountStatementQuery request, CancellationToken cancellationToken)
    {
        var all = await accounts.GetByFarmIdAsync(request.FarmId, includeInactive: true, cancellationToken);
        var account = request.AccountId is { } id
            ? all.FirstOrDefault(a => a.Id == id) ?? throw new NotFoundException(nameof(MoneyAccount), id)
            : all.FirstOrDefault(a => a.IsDefault) ?? throw new NotFoundException(nameof(MoneyAccount), "default");

        var to = request.ToDate ?? _clock.Today;
        var from = request.FromDate ?? new DateOnly(to.Year, to.Month, 1);
        if (to < from)
            (from, to) = (to, from);

        var ledger = await engine.LoadAsync(request.FarmId, to, cancellationToken);
        return Build(ledger, all, account, from, to);
    }

    /// <summary>The statement (pure, so the cash-book report reuses it).</summary>
    public static MoneyAccountStatementDto Build(FarmLedger ledger, IReadOnlyList<MoneyAccount> accounts, MoneyAccount account, DateOnly from, DateOnly to)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(account);

        var defaultId = accounts.FirstOrDefault(a => a.IsDefault)?.Id ?? Guid.Empty;
        var names = accounts.ToDictionary(a => a.Id, a => a.Name);
        var opening = MoneyAccountBalances.Compute(ledger, accounts, from.AddDays(-1)).GetValueOrDefault(account.Id);
        var byId = ledger.AllPostings.ToDictionary(t => t.Id);

        var entries = new List<(DateTime Date, DateTime Created, MoneyAccountLineDto Line)>();

        // Opening cash entered for the books start, dated inside the period (default account only).
        if (account.Id == defaultId)
        {
            foreach (var line in ledger.Lines.Where(l => l.Account == AccountChart.CashAccount
                                                         && l.Source != LedgerLineSource.Posting
                                                         && l.Date >= from && l.Date <= to && l.Amount != 0m))
            {
                var date = line.Date.ToDateTime(TimeOnly.MinValue);
                entries.Add((date, DateTime.MinValue, new MoneyAccountLineDto(
                    Guid.Empty, date, "OPENING", "Opening cash balance", "Opening", "Opening balance", "প্রারম্ভিক জের",
                    Math.Max(0m, line.Amount), Math.Max(0m, -line.Amount), 0m, false, null, null, null)));
            }
        }

        foreach (var t in ledger.AllPostings)
        {
            var day = DateOnly.FromDateTime(t.TransactionDate);
            if (!t.MovesMoneyAccount || !ledger.IsInBooks(t) || day < from || day > to
                || MoneyAccountBalances.AccountOf(t, defaultId) != account.Id)
                continue;

            var effect = AccountChart.AccountCashEffect(t);
            var head = AccountChart.For(t.Category);
            string? counter = null;
            if (t.IsAccountTransfer && t.LinkedTransactionId is { } linked && byId.TryGetValue(linked, out var other))
                counter = names.GetValueOrDefault(MoneyAccountBalances.AccountOf(other, defaultId));

            entries.Add((t.TransactionDate, t.CreatedAtUtc, new MoneyAccountLineDto(
                t.Id, t.TransactionDate, AccountChart.VoucherNo(t), AccountChart.Describe(t), t.Category.ToString(),
                head.Name, head.NameBn, Math.Max(0m, effect), Math.Max(0m, -effect), 0m,
                t.IsAccountTransfer, counter, t.LinkedTransactionId, t.SourceModule)));
        }

        var running = opening;
        var lines = new List<MoneyAccountLineDto>();
        foreach (var e in entries.OrderBy(e => e.Date).ThenBy(e => e.Created))
        {
            running += e.Line.InBdt - e.Line.OutBdt;
            lines.Add(e.Line with { BalanceBdt = Math.Round(running, 2) });
        }

        var totalIn = lines.Sum(l => l.InBdt);
        var totalOut = lines.Sum(l => l.OutBdt);
        return new MoneyAccountStatementDto(
            account.ToDto(MoneyAccountBalances.Compute(ledger, accounts, to).GetValueOrDefault(account.Id)),
            from, to, Math.Round(opening, 2), totalIn, totalOut, Math.Round(opening + totalIn - totalOut, 2), lines);
    }
}
