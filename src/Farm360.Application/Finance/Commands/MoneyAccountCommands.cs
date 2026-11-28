using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Behaviors;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Repositories;
using Farm360.Application.Finance.Services;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Interfaces;
using MediatR;

namespace Farm360.Application.Finance.Commands;

// ══════════════════════════════════════════════════════════════════════════════
// MONEY ACCOUNTS — cash in hand, bank, mobile wallet
// ══════════════════════════════════════════════════════════════════════════════

public sealed record CreateMoneyAccountCommand(Guid TenantId, Guid FarmId, SaveMoneyAccountRequest Request) : IRequest<MoneyAccountDto>;

public sealed record UpdateMoneyAccountCommand(Guid FarmId, Guid AccountId, SaveMoneyAccountRequest Request) : IRequest<MoneyAccountDto>;

/// <summary>Closes (or reopens) an account. A closed account keeps its history but takes no new money.</summary>
public sealed record SetMoneyAccountActiveCommand(Guid FarmId, Guid AccountId, bool IsActive) : IRequest<MoneyAccountDto>;

/// <summary>Makes an account the farm's default (where postings that name no account land).</summary>
public sealed record SetDefaultMoneyAccountCommand(Guid FarmId, Guid AccountId) : IRequest<MoneyAccountDto>, ITransactionalCommand;

public sealed record TransferBetweenAccountsCommand(Guid TenantId, Guid FarmId, TransferBetweenAccountsRequest Request)
    : IRequest<MoneyTransferDto>, ITransactionalCommand;

/// <summary>Removes a transfer (both legs), given either leg's id.</summary>
public sealed record DeleteMoneyTransferCommand(Guid FarmId, Guid TransactionId) : IRequest, ITransactionalCommand;

internal static class MoneyAccountMapping
{
    public const string Module = "MoneyAccounts";

    public static MoneyAccountKind ParseKind(string? kind) =>
        Enum.TryParse<MoneyAccountKind>(kind, true, out var k) && Enum.IsDefined(k)
            ? k
            : throw new ArgumentException($"Invalid account kind: '{kind}'. Expected Cash, Bank or MobileWallet.");

    public static MoneyAccountDto ToDto(this MoneyAccount a, decimal balanceBdt) =>
        new(a.Id, a.FarmId, a.Name, a.Kind.ToString(), a.ProviderName, a.AccountNumber, a.Branch, a.Notes,
            a.IsDefault, a.IsActive, Math.Round(balanceBdt, 2));

    public static async Task<MoneyAccount> LoadAsync(IMoneyAccountRepository accounts, Guid farmId, Guid accountId, CancellationToken ct)
    {
        var account = await accounts.GetByIdAsync(accountId, ct);
        if (account is null || account.FarmId != farmId)
            throw new NotFoundException(nameof(MoneyAccount), accountId);
        return account;
    }

    public static async Task<MoneyAccountDto> WithBalanceAsync(IMoneyAccountService money, MoneyAccount account, CancellationToken ct)
    {
        var balances = await money.GetBalancesAsync(account.FarmId, cancellationToken: ct);
        return account.ToDto(balances.FirstOrDefault(b => b.Account.Id == account.Id)?.BalanceBdt ?? 0m);
    }
}

public sealed class CreateMoneyAccountCommandHandler(
    IMoneyAccountRepository accounts,
    IMoneyAccountService money,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateMoneyAccountCommand, MoneyAccountDto>
{
    public async Task<MoneyAccountDto> Handle(CreateMoneyAccountCommand command, CancellationToken cancellationToken)
    {
        var r = command.Request;
        var existing = await accounts.GetByFarmIdAsync(command.FarmId, includeInactive: true, cancellationToken);
        if (existing.Any(a => string.Equals(a.Name, r.Name?.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new ConflictException($"This farm already has a money account called '{r.Name?.Trim()}'.");

        // Make sure the farm's default exists first, so a new account never becomes the only one silently.
        await money.GetDefaultAsync(command.TenantId, command.FarmId, cancellationToken);

        var account = MoneyAccount.Create(command.TenantId, command.FarmId, r.Name ?? string.Empty,
            MoneyAccountMapping.ParseKind(r.Kind), r.ProviderName, r.AccountNumber, r.Branch, r.Notes);
        accounts.Add(account);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return account.ToDto(0m);
    }
}

public sealed class UpdateMoneyAccountCommandHandler(
    IMoneyAccountRepository accounts,
    IMoneyAccountService money,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateMoneyAccountCommand, MoneyAccountDto>
{
    public async Task<MoneyAccountDto> Handle(UpdateMoneyAccountCommand command, CancellationToken cancellationToken)
    {
        var account = await MoneyAccountMapping.LoadAsync(accounts, command.FarmId, command.AccountId, cancellationToken);
        var r = command.Request;
        var others = await accounts.GetByFarmIdAsync(command.FarmId, includeInactive: true, cancellationToken);
        if (others.Any(a => a.Id != account.Id && string.Equals(a.Name, r.Name?.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new ConflictException($"This farm already has a money account called '{r.Name?.Trim()}'.");

        account.Update(r.Name ?? string.Empty, MoneyAccountMapping.ParseKind(r.Kind), r.ProviderName, r.AccountNumber, r.Branch, r.Notes);
        accounts.Update(account);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await MoneyAccountMapping.WithBalanceAsync(money, account, cancellationToken);
    }
}

public sealed class SetMoneyAccountActiveCommandHandler(
    IMoneyAccountRepository accounts,
    IMoneyAccountService money,
    IUnitOfWork unitOfWork) : IRequestHandler<SetMoneyAccountActiveCommand, MoneyAccountDto>
{
    public async Task<MoneyAccountDto> Handle(SetMoneyAccountActiveCommand command, CancellationToken cancellationToken)
    {
        var account = await MoneyAccountMapping.LoadAsync(accounts, command.FarmId, command.AccountId, cancellationToken);
        if (command.IsActive)
        {
            account.Activate();
        }
        else
        {
            var balance = (await MoneyAccountMapping.WithBalanceAsync(money, account, cancellationToken)).BalanceBdt;
            if (Math.Abs(balance) >= 0.01m)
                throw new InvalidOperationException(
                    $"'{account.Name}' still holds {balance:N2} BDT. Transfer it to another account before closing this one.");
            account.Deactivate();
        }

        accounts.Update(account);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await MoneyAccountMapping.WithBalanceAsync(money, account, cancellationToken);
    }
}

public sealed class SetDefaultMoneyAccountCommandHandler(
    IMoneyAccountRepository accounts,
    IMoneyAccountService money,
    IFinancialTransactionRepository transactions,
    IUnitOfWork unitOfWork) : IRequestHandler<SetDefaultMoneyAccountCommand, MoneyAccountDto>
{
    public async Task<MoneyAccountDto> Handle(SetDefaultMoneyAccountCommand command, CancellationToken cancellationToken)
    {
        var account = await MoneyAccountMapping.LoadAsync(accounts, command.FarmId, command.AccountId, cancellationToken);
        if (account.IsDefault)
            return await MoneyAccountMapping.WithBalanceAsync(money, account, cancellationToken);

        // Rows with no account belong to the default. Pin them to the old default before it changes,
        // so moving the flag never moves money between accounts.
        var previous = (await accounts.GetByFarmIdAsync(command.FarmId, includeInactive: true, cancellationToken))
            .FirstOrDefault(a => a.IsDefault);
        if (previous is not null)
        {
            foreach (var t in (await transactions.GetAllByFarmIdAsync(command.FarmId, cancellationToken))
                         .Where(t => t.MovesMoneyAccount && t.MoneyAccountId is null))
            {
                t.AssignMoneyAccount(previous.Id);
                await transactions.UpdateAsync(t, cancellationToken);
            }

            previous.SetDefault(false);
            accounts.Update(previous);
            // Clear the old flag first: one default per farm is enforced by a unique index.
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        account.SetDefault(true);
        accounts.Update(account);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await MoneyAccountMapping.WithBalanceAsync(money, account, cancellationToken);
    }
}

/// <summary>
/// A transfer is two postings of <see cref="TransactionCategory.AccountTransfer"/>: out of one account
/// (Expense-type) and into the other (Income-type), linked to each other. Each account's balance
/// moves; total cash, profit and the balance sheet do not.
/// </summary>
public sealed class TransferBetweenAccountsCommandHandler(
    IMoneyAccountService money,
    IFinancialTransactionRepository transactions,
    IUnitOfWork unitOfWork) : IRequestHandler<TransferBetweenAccountsCommand, MoneyTransferDto>
{
    public async Task<MoneyTransferDto> Handle(TransferBetweenAccountsCommand command, CancellationToken cancellationToken)
    {
        var r = command.Request;
        if (r.AmountBdt <= 0)
            throw new ArgumentException("Enter an amount greater than zero.");
        if (r.FromAccountId == r.ToAccountId)
            throw new ArgumentException("Choose two different accounts to transfer between.");

        var from = await money.ResolveAsync(command.TenantId, command.FarmId, r.FromAccountId, cancellationToken);
        var to = await money.ResolveAsync(command.TenantId, command.FarmId, r.ToAccountId, cancellationToken);
        var amount = Math.Round(r.AmountBdt, 2);

        await money.WarnIfShortAsync(from, amount, cancellationToken: cancellationToken);

        var transferId = Guid.NewGuid();
        var reference = string.IsNullOrWhiteSpace(r.ReferenceId)
            ? $"TRF-{transferId.ToString("N")[..6].ToUpperInvariant()}"
            : r.ReferenceId.Trim();

        var outLeg = FinancialTransaction.Create(
            command.TenantId, command.FarmId, TransactionType.Expense, TransactionCategory.AccountTransfer, amount, r.TransferDate,
            reference, r.Notes ?? string.Empty, $"Transfer to {to.Name}",
            isAutomated: true, sourceModule: MoneyAccountMapping.Module, settlement: SettlementAccount.Cash,
            sourceDocumentId: transferId, moneyAccountId: from.Id);
        var inLeg = FinancialTransaction.Create(
            command.TenantId, command.FarmId, TransactionType.Income, TransactionCategory.AccountTransfer, amount, r.TransferDate,
            reference, r.Notes ?? string.Empty, $"Transfer from {from.Name}",
            isAutomated: true, sourceModule: MoneyAccountMapping.Module, settlement: SettlementAccount.Cash,
            sourceDocumentId: LedgerPosting.SubKey(transferId, "in"), moneyAccountId: to.Id);
        outLeg.LinkTo(inLeg.Id);
        inLeg.LinkTo(outLeg.Id);

        await transactions.AddAsync(outLeg, cancellationToken);
        await transactions.AddAsync(inLeg, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new MoneyTransferDto(outLeg.Id, inLeg.Id, from.Id, to.Id, amount, r.TransferDate, reference);
    }
}

public sealed class DeleteMoneyTransferCommandHandler(
    IFinancialTransactionRepository transactions,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteMoneyTransferCommand>
{
    public async Task Handle(DeleteMoneyTransferCommand command, CancellationToken cancellationToken)
    {
        var leg = await transactions.GetByIdAsync(command.TransactionId, cancellationToken);
        if (leg is null || leg.FarmId != command.FarmId || !leg.IsAccountTransfer)
            throw new NotFoundException("MoneyTransfer", command.TransactionId);

        if (leg.LinkedTransactionId is { } otherId
            && await transactions.GetByIdAsync(otherId, cancellationToken) is { } other
            && other.FarmId == leg.FarmId && other.IsAccountTransfer)
        {
            await transactions.DeleteAsync(other, cancellationToken);
        }

        await transactions.DeleteAsync(leg, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
