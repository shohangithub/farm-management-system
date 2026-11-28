using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Repositories;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Application.Finance.Services;
using MediatR;

namespace Farm360.Application.Finance.Commands;

public record RecordExpenseCommand(
    Guid TenantId,
    Guid FarmId,
    string Category,
    decimal AmountBdt,
    DateTime TransactionDate,
    string Description,
    string ReferenceId,
    string Notes,
    Guid? AnimalId,
    Guid? BatchId,
    Guid? ShedId,
    CreditTermsRequest? Credit = null,
    Guid? MoneyAccountId = null,
    Guid? PaidByPartnerId = null
) : IRequest<FinancialTransactionDto>;

public class RecordExpenseCommandHandler : IRequestHandler<RecordExpenseCommand, FinancialTransactionDto>
{
    private readonly IFinancialTransactionRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITradeCreditService _credit;
    private readonly IPublisher _publisher;
    private readonly Farm360.Domain.Livestock.Repositories.IAnimalRepository? _animals;
    private readonly IMoneyAccountService? _money;

    public RecordExpenseCommandHandler(IFinancialTransactionRepository repository, IUnitOfWork unitOfWork, ITradeCreditService credit, IPublisher publisher,
        Farm360.Domain.Livestock.Repositories.IAnimalRepository? animals = null, IMoneyAccountService? money = null)
    {
        _money = money;
        _animals = animals;
        _repository = repository;
        _unitOfWork = unitOfWork;
        _credit = credit;
        _publisher = publisher;
    }

    public async Task<FinancialTransactionDto> Handle(RecordExpenseCommand request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<TransactionCategory>(request.Category, true, out var category))
            throw new ArgumentException($"Invalid transaction category: {request.Category}");

        AccountChart.EnsureManualAllowed(TransactionType.Expense, category);
        await AnimalExpenseGuard.EnsureAsync(_animals, request.AnimalId, request.FarmId, request.TransactionDate, cancellationToken);

        var transaction = FinancialTransaction.Create(
            request.TenantId,
            request.FarmId,
            TransactionType.Expense,
            category,
            request.AmountBdt,
            request.TransactionDate,
            request.ReferenceId,
            request.Notes,
            request.Description,
            request.AnimalId,
            request.BatchId,
            request.ShedId
        );

        await _repository.AddAsync(transaction, cancellationToken);

        // Not paid on the day: the supplier is owed (Accounts Payable) until settled.
        if (request.Credit is { } credit)
        {
            await _credit.OpenAsync(transaction, new CreditTerms(
                credit.PartyName, credit.PartyPhone, credit.DueDate, credit.PaidNowBdt, credit.PaymentMethod, credit.Notes,
                MoneyAccountId: credit.MoneyAccountId ?? request.MoneyAccountId),
                cancellationToken);
        }
        else if (_money is not null)
        {
            // Paid from the chosen account, or directly by a partner (books their capital too).
            await _money.ApplyAsync(transaction, new PaymentSource(request.MoneyAccountId, request.PaidByPartnerId), cancellationToken);
        }

        var domainEvents = transaction.DomainEvents.ToList();
        transaction.ClearDomainEvents();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Domain events are published by hand in this codebase. Without this, an expense tagged to an
        // animal never reached that animal's cost ledger (AnimalCostLedgerUpdater).
        foreach (var domainEvent in domainEvents)
        {
            await _publisher.Publish(domainEvent, cancellationToken);
        }

        return transaction.ToDto();
    }
}
