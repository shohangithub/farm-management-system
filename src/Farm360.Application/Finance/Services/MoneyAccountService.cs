using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Repositories;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Interfaces;

namespace Farm360.Application.Finance.Services;

/// <summary>
/// Where the money for a spend came from, or where money received went: a money account (null =
/// the farm's default), or a partner who paid the spend out of their own pocket. "On credit" is
/// not a source — a credit deal moves no money until it is paid, and the payment names its account.
/// </summary>
public sealed record PaymentSource(Guid? MoneyAccountId = null, Guid? PartnerId = null)
{
    public static PaymentSource Default { get; } = new();

    public bool IsPartner => PartnerId is not null;

    public bool IsSpecified => MoneyAccountId is not null || PartnerId is not null;
}

/// <summary>A money account and its balance.</summary>
public sealed record MoneyAccountBalance(MoneyAccount Account, decimal BalanceBdt);

/// <summary>
/// The farm's money accounts: picking the account a posting goes through, the balance of each,
/// spends paid directly by a partner, and the "not enough money" warning.
/// </summary>
/// <remarks>Nothing here saves; callers commit with their unit of work.</remarks>
public interface IMoneyAccountService
{
    /// <summary>The farm's default account, created (in this unit of work) when the farm has none yet.</summary>
    Task<MoneyAccount> GetDefaultAsync(Guid tenantId, Guid farmId, CancellationToken cancellationToken = default);

    /// <summary>The chosen account (checked: this farm's and open), or the default when none is chosen.</summary>
    Task<MoneyAccount> ResolveAsync(Guid tenantId, Guid farmId, Guid? moneyAccountId, CancellationToken cancellationToken = default);

    /// <summary>Every account of the farm with its balance (opening cash included in the default account).</summary>
    Task<IReadOnlyList<MoneyAccountBalance>> GetBalancesAsync(Guid farmId, DateOnly? asOf = null, Guid? excludeTransactionId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts a cash posting through its source: the chosen account, or — paid directly by a partner —
    /// the default account plus that partner's capital contribution of the same amount and date, so
    /// cash nets to zero. Warns (never blocks) when a spend is more than the account holds.
    /// Non-cash postings (on credit, from stock) are left alone.
    /// </summary>
    Task ApplyAsync(FinancialTransaction posting, PaymentSource source, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="ApplyAsync(FinancialTransaction, PaymentSource, CancellationToken)"/>
    /// <param name="warn">False when the caller warns once for several postings itself.</param>
    Task ApplyAsync(FinancialTransaction posting, PaymentSource source, bool warn, CancellationToken cancellationToken = default);

    /// <summary><see cref="ApplyAsync"/> for the posting a module produced for a source document.</summary>
    Task<FinancialTransaction?> ApplyToSourceAsync(Guid farmId, Guid sourceDocumentId, TransactionCategory category, PaymentSource source, CancellationToken cancellationToken = default);

    /// <summary>Keeps a partner-paid spend's contribution in step after the spend was revised.</summary>
    Task SyncLinkedAsync(FinancialTransaction posting, CancellationToken cancellationToken = default);

    /// <summary>The spend is being removed: removes the partner contribution booked for it, if any.</summary>
    Task RemoveLinkedAsync(FinancialTransaction posting, CancellationToken cancellationToken = default);

    /// <summary>Adds a non-blocking warning when <paramref name="amountBdt"/> is more than the account holds.</summary>
    Task WarnIfShortAsync(MoneyAccount account, decimal amountBdt, Guid? excludeTransactionId = null, CancellationToken cancellationToken = default);
}

public sealed class MoneyAccountService : IMoneyAccountService
{
    public const string PartnerModule = "Partners";

    private readonly IMoneyAccountRepository _accounts;
    private readonly IFinancialTransactionRepository _transactions;
    private readonly IFinancialStatementEngine _engine;
    private readonly IInvestorRepository? _investors;
    private readonly IOperationWarnings? _warnings;

    public MoneyAccountService(
        IMoneyAccountRepository accounts,
        IFinancialTransactionRepository transactions,
        IFinancialStatementEngine engine,
        IInvestorRepository? investors = null,
        IOperationWarnings? warnings = null)
    {
        _accounts = accounts;
        _transactions = transactions;
        _engine = engine;
        _investors = investors;
        _warnings = warnings;
    }

    public async Task<MoneyAccount> GetDefaultAsync(Guid tenantId, Guid farmId, CancellationToken cancellationToken = default)
    {
        var existing = await _accounts.GetDefaultAsync(farmId, cancellationToken);
        if (existing is not null)
            return existing;

        var created = MoneyAccount.CreateDefault(tenantId, farmId);
        _accounts.Add(created);
        return created;
    }

    public async Task<MoneyAccount> ResolveAsync(Guid tenantId, Guid farmId, Guid? moneyAccountId, CancellationToken cancellationToken = default)
    {
        if (moneyAccountId is not { } id || id == Guid.Empty)
            return await GetDefaultAsync(tenantId, farmId, cancellationToken);

        var account = await _accounts.GetByIdAsync(id, cancellationToken);
        if (account is null || account.FarmId != farmId)
            throw new NotFoundException(nameof(MoneyAccount), id);
        if (!account.IsActive)
            throw new InvalidOperationException($"The money account '{account.Name}' is closed. Reopen it or choose another account.");
        return account;
    }

    public async Task<IReadOnlyList<MoneyAccountBalance>> GetBalancesAsync(
        Guid farmId, DateOnly? asOf = null, Guid? excludeTransactionId = null, CancellationToken cancellationToken = default)
    {
        var accounts = await _accounts.GetByFarmIdAsync(farmId, includeInactive: true, cancellationToken);
        // "Now" counts every posting, whatever its date (Compute reads them all). The statement lines
        // around them (opening cash, depreciation, carried costs) are loaded to a year ahead: loading
        // to DateOnly.MaxValue overflowed the date arithmetic there and crashed every balance lookup.
        var day = asOf ?? DateOnly.MaxValue;
        var loadUpTo = asOf ?? DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1);
        var ledger = await _engine.LoadAsync(farmId, loadUpTo, cancellationToken);
        var balances = MoneyAccountBalances.Compute(ledger, accounts, day, excludeTransactionId);
        return accounts.Select(a => new MoneyAccountBalance(a, balances.GetValueOrDefault(a.Id))).ToList();
    }

    public Task ApplyAsync(FinancialTransaction posting, PaymentSource source, CancellationToken cancellationToken = default) =>
        ApplyAsync(posting, source, warn: true, cancellationToken);

    public async Task ApplyAsync(FinancialTransaction posting, PaymentSource source, bool warn, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(posting);
        ArgumentNullException.ThrowIfNull(source);

        if (!posting.MovesMoneyAccount)
        {
            // Put on credit (or never cash): no money moved, so no partner paid it either.
            await RemoveLinkedAsync(posting, cancellationToken);
            return;
        }

        if (source.PartnerId is { } partnerId)
        {
            if (posting.Type != TransactionType.Expense)
                throw new InvalidOperationException("Only a payment can be made directly by a partner. Money received goes into one of the farm's accounts.");

            var defaultAccount = await GetDefaultAsync(posting.TenantId, posting.FarmId, cancellationToken);
            posting.AssignMoneyAccount(defaultAccount.Id);
            await BookPartnerPaymentAsync(posting, partnerId, cancellationToken);
            return;
        }

        var account = await ResolveAsync(posting.TenantId, posting.FarmId, source.MoneyAccountId, cancellationToken);
        posting.AssignMoneyAccount(account.Id);

        // The partner no longer paid it (the source changed to an account).
        await RemoveLinkedAsync(posting, cancellationToken);

        if (warn && AccountChart.AccountCashEffect(posting) < 0)
            await WarnIfShortAsync(account, posting.AmountBdt, posting.Id, cancellationToken);
    }

    public async Task<FinancialTransaction?> ApplyToSourceAsync(
        Guid farmId, Guid sourceDocumentId, TransactionCategory category, PaymentSource source, CancellationToken cancellationToken = default)
    {
        var posting = await _transactions.FindBySourceAsync(farmId, sourceDocumentId, category, cancellationToken);
        if (posting is null)
            return null;

        await ApplyAsync(posting, source, cancellationToken);
        await _transactions.UpdateAsync(posting, cancellationToken);
        return posting;
    }

    public async Task SyncLinkedAsync(FinancialTransaction posting, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(posting);
        var contribution = await FindContributionForAsync(posting, cancellationToken);
        if (contribution is null)
            return;

        if (!posting.MovesMoneyAccount)
        {
            await RemoveLinkedAsync(posting, cancellationToken);
            return;
        }

        if (contribution.AmountBdt == posting.AmountBdt && contribution.TransactionDate == posting.TransactionDate
            && contribution.MoneyAccountId == posting.MoneyAccountId)
            return;

        contribution.Revise(posting.AmountBdt, posting.TransactionDate, contribution.Description, contribution.Notes, SettlementAccount.Cash);
        contribution.AssignMoneyAccount(posting.MoneyAccountId);
        await _transactions.UpdateAsync(contribution, cancellationToken);

        var (investor, investorTx) = await FindInvestorTransactionAsync(contribution, cancellationToken);
        if (investor is not null && investorTx is not null)
        {
            investor.ReviseContribution(investorTx.Id, posting.AmountBdt, posting.TransactionDate);
            _investors!.Update(investor);
        }
    }

    public async Task RemoveLinkedAsync(FinancialTransaction posting, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(posting);
        var contribution = await FindContributionForAsync(posting, cancellationToken);
        if (contribution is null)
            return;

        var (investor, investorTx) = await FindInvestorTransactionAsync(contribution, cancellationToken);
        if (investor is not null && investorTx is not null)
        {
            investor.RemoveTransaction(investorTx.Id);
            _investors!.Update(investor);
        }

        await _transactions.DeleteAsync(contribution, cancellationToken);
        if (posting.LinkedTransactionId == contribution.Id)
            posting.LinkTo(null);
    }

    public async Task WarnIfShortAsync(MoneyAccount account, decimal amountBdt, Guid? excludeTransactionId = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (_warnings is null || amountBdt <= 0)
            return;

        var balances = await GetBalancesAsync(account.FarmId, excludeTransactionId: excludeTransactionId, cancellationToken: cancellationToken);
        var balance = balances.FirstOrDefault(b => b.Account.Id == account.Id)?.BalanceBdt ?? 0m;
        if (amountBdt > balance + 0.005m)
            _warnings.Add(ShortMessage(account.Name, balance, amountBdt));
    }

    /// <summary>"Bank has ৳40,000; this spends ৳60,000." — translated by the message catalogue.</summary>
    public static string ShortMessage(string accountName, decimal balanceBdt, decimal amountBdt) =>
        string.Create(CultureInfo.InvariantCulture, $"{accountName} has ৳{balanceBdt:N0}; this spends ৳{amountBdt:N0}. Add partner money or transfer between accounts.");

    // ── Partner paid directly ───────────────────────────────────────────────

    private async Task BookPartnerPaymentAsync(FinancialTransaction spend, Guid partnerId, CancellationToken cancellationToken)
    {
        if (_investors is null)
            throw new InvalidOperationException("Partners are not available here.");

        var investor = await _investors.GetByIdWithTransactionsAsync(partnerId, cancellationToken);
        if (investor is null || investor.FarmId != spend.FarmId || investor.TenantId != spend.TenantId)
            throw new NotFoundException(nameof(Investor), partnerId);
        if (!investor.IsActive)
            throw new InvalidOperationException($"{investor.Name} is not an active partner. Reactivate them first.");

        var existing = await FindContributionForAsync(spend, cancellationToken);
        if (existing is not null)
        {
            var (owner, ownerTx) = await FindInvestorTransactionAsync(existing, cancellationToken);
            if (owner?.Id == investor.Id && ownerTx is not null)
            {
                await SyncLinkedAsync(spend, cancellationToken);
                return;
            }

            await RemoveLinkedAsync(spend, cancellationToken);
        }

        var what = AccountChart.Describe(spend);
        var contribution = FinancialTransaction.Create(
            spend.TenantId,
            spend.FarmId,
            TransactionType.Income,
            TransactionCategory.InvestorCapital,
            spend.AmountBdt,
            spend.TransactionDate,
            referenceId: spend.ReferenceId,
            notes: $"Paid directly by {investor.Name}: {what}",
            description: $"Capital from partner {investor.Name} (paid directly)",
            isAutomated: true,
            sourceModule: PartnerModule,
            settlement: SettlementAccount.Cash,
            sourceDocumentId: spend.Id,
            moneyAccountId: spend.MoneyAccountId);
        contribution.LinkTo(spend.Id);
        spend.LinkTo(contribution.Id);
        await _transactions.AddAsync(contribution, cancellationToken);

        investor.RecordTransaction(
            spend.TenantId,
            InvestorTransactionType.CapitalContribution,
            spend.AmountBdt,
            spend.TransactionDate,
            spend.ReferenceId,
            $"Paid directly: {what}",
            contribution.Id);
        _investors.Update(investor);
    }

    private async Task<FinancialTransaction?> FindContributionForAsync(FinancialTransaction spend, CancellationToken cancellationToken)
    {
        if (spend.IsAccountTransfer || spend.Category == TransactionCategory.InvestorCapital)
            return null;
        var contribution = await _transactions.FindBySourceAsync(spend.FarmId, spend.Id, TransactionCategory.InvestorCapital, cancellationToken);
        return contribution is not null && contribution.SourceModule == PartnerModule ? contribution : null;
    }

    private async Task<(Investor? Investor, InvestorTransaction? Transaction)> FindInvestorTransactionAsync(
        FinancialTransaction contribution, CancellationToken cancellationToken)
    {
        if (_investors is null)
            return (null, null);

        foreach (var investor in await _investors.GetByFarmIdAsync(contribution.FarmId, includeTransactions: true, cancellationToken))
        {
            var tx = investor.Transactions.FirstOrDefault(t => t.FinancialTransactionId == contribution.Id);
            if (tx is not null)
            {
                // Reload tracked with its transactions so the change is saved with the unit of work.
                var tracked = await _investors.GetByIdWithTransactionsAsync(investor.Id, cancellationToken) ?? investor;
                return (tracked, tracked.Transactions.FirstOrDefault(t => t.Id == tx.Id));
            }
        }

        return (null, null);
    }
}

/// <summary>Per-account balances from the farm's ledger (pure; see <see cref="Compute"/>).</summary>
public static class MoneyAccountBalances
{
    /// <summary>
    /// Balance of each money account at the end of <paramref name="asOf"/>: the sum of the cash
    /// effects of its postings in the books. Postings with no account, and the opening cash of the
    /// books, belong to the default account. The total always equals the ledger's Cash &amp; Bank.
    /// </summary>
    public static Dictionary<Guid, decimal> Compute(FarmLedger ledger, IReadOnlyList<MoneyAccount> accounts, DateOnly asOf, Guid? excludeTransactionId = null)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(accounts);

        var defaultId = accounts.FirstOrDefault(a => a.IsDefault)?.Id ?? Guid.Empty;
        var balances = accounts.ToDictionary(a => a.Id, _ => 0m);

        // Opening cash (and anything else posted straight to Cash & Bank outside the postings).
        var opening = ledger.Lines
            .Where(l => l.Date <= asOf && l.Account == AccountChart.CashAccount && l.Source != LedgerLineSource.Posting)
            .Sum(l => l.Amount);
        if (defaultId != Guid.Empty)
            balances[defaultId] = balances.GetValueOrDefault(defaultId) + opening;

        foreach (var t in ledger.AllPostings)
        {
            if (!t.MovesMoneyAccount || t.Id == excludeTransactionId || !ledger.IsInBooks(t)
                || DateOnly.FromDateTime(t.TransactionDate) > asOf)
                continue;

            var id = t.MoneyAccountId ?? defaultId;
            balances[id] = balances.GetValueOrDefault(id) + AccountChart.AccountCashEffect(t);
        }

        return balances;
    }

    /// <summary>Which account a posting's money went through (null = default).</summary>
    public static Guid AccountOf(FinancialTransaction t, Guid defaultAccountId)
    {
        ArgumentNullException.ThrowIfNull(t);
        return t.MoneyAccountId ?? defaultAccountId;
    }
}

/// <summary>Cash &amp; bank split by money account, for dashboards and reports.</summary>
public static class MoneyAccountBreakdown
{
    public static async Task<IReadOnlyList<Farm360.Contracts.Finance.MoneyAccountBreakdownDto>?> ForAsync(
        IMoneyAccountRepository? repository, FarmLedger ledger, DateOnly asOf, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        if (repository is null)
            return null;

        var accounts = await repository.GetByFarmIdAsync(ledger.FarmId, includeInactive: true, cancellationToken);
        if (accounts.Count == 0)
            return null;

        var balances = MoneyAccountBalances.Compute(ledger, accounts, asOf);
        return accounts
            .Select(a => (Account: a, Balance: Math.Round(balances.GetValueOrDefault(a.Id), 2)))
            .Where(x => x.Account.IsActive || x.Balance != 0m)
            .OrderByDescending(x => x.Account.IsDefault)
            .ThenBy(x => x.Account.Kind)
            .ThenBy(x => x.Account.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(x => new Farm360.Contracts.Finance.MoneyAccountBreakdownDto(
                x.Account.Id, x.Account.Name, x.Account.Kind.ToString(), x.Account.IsDefault, x.Balance))
            .ToList();
    }
}
