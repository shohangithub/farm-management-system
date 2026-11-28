using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Repositories;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Interfaces;
using MediatR;

namespace Farm360.Application.Finance.Commands;

public record RecordInvestorTransactionCommand(
    Guid TenantId,
    Guid FarmId,
    Guid InvestorId,
    string Type,
    decimal AmountBdt,
    DateTime TransactionDate,
    string? ReferenceId = null,
    string? Notes = null,
    Guid? MoneyAccountId = null
) : IRequest<InvestorTransactionDto>;

public class RecordInvestorTransactionCommandHandler : IRequestHandler<RecordInvestorTransactionCommand, InvestorTransactionDto>
{
    private readonly IInvestorRepository _investorRepository;
    private readonly IFinancialTransactionRepository _transactionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly Farm360.Application.Finance.Services.IMoneyAccountService? _money;

    public RecordInvestorTransactionCommandHandler(
        IInvestorRepository investorRepository,
        IFinancialTransactionRepository transactionRepository,
        IUnitOfWork unitOfWork,
        Farm360.Application.Finance.Services.IMoneyAccountService? money = null)
    {
        _investorRepository = investorRepository;
        _transactionRepository = transactionRepository;
        _unitOfWork = unitOfWork;
        _money = money;
    }

    public async Task<InvestorTransactionDto> Handle(RecordInvestorTransactionCommand request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<InvestorTransactionType>(request.Type, true, out var txType))
            throw new ArgumentException($"Invalid investor transaction type: '{request.Type}'. Expected 'CapitalContribution', 'CapitalWithdrawal', or 'ProfitDistribution'.", nameof(request));

        var investor = await _investorRepository.GetByIdWithTransactionsAsync(request.InvestorId, cancellationToken)
            ?? throw new NotFoundException(nameof(Investor), request.InvestorId);

        if (investor.TenantId != request.TenantId)
            throw new NotFoundException(nameof(Investor), request.InvestorId);

        if (investor.FarmId != request.FarmId)
            throw new ArgumentException("Investor does not belong to the specified farm.", nameof(request));

        // 1. Post to General Ledger
        TransactionType glType;
        TransactionCategory glCategory;
        string glDescription;

        switch (txType)
        {
            case InvestorTransactionType.CapitalContribution:
                glType = TransactionType.Income;
                glCategory = TransactionCategory.InvestorCapital;
                glDescription = $"Capital contribution from partner {investor.Name}";
                break;

            case InvestorTransactionType.CapitalWithdrawal:
                glType = TransactionType.Expense;
                glCategory = TransactionCategory.InvestorWithdrawal;
                glDescription = $"Capital withdrawal by partner {investor.Name}";
                break;

            case InvestorTransactionType.ProfitDistribution:
                glType = TransactionType.Expense;
                glCategory = TransactionCategory.ProfitDistribution;
                glDescription = $"Profit paid to partner {investor.Name}";
                break;

            default:
                throw new InvalidOperationException($"Unsupported transaction type: {txType}");
        }

        // Money in goes INTO the chosen account; a withdrawal or payout comes OUT of it.
        var account = _money is null
            ? null
            : await _money.ResolveAsync(request.TenantId, request.FarmId, request.MoneyAccountId, cancellationToken);
        if (account is not null && glType == TransactionType.Expense)
            await _money!.WarnIfShortAsync(account, request.AmountBdt, cancellationToken: cancellationToken);

        var glTx = FinancialTransaction.Create(
            request.TenantId,
            request.FarmId,
            glType,
            glCategory,
            request.AmountBdt,
            request.TransactionDate,
            request.ReferenceId ?? string.Empty,
            request.Notes ?? string.Empty,
            glDescription,
            isAutomated: true,
            sourceModule: "Investors",
            moneyAccountId: account?.Id
        );

        await _transactionRepository.AddAsync(glTx, cancellationToken);

        // 2. Record on Investor aggregate
        var recordedTx = investor.RecordTransaction(
            request.TenantId,
            txType,
            request.AmountBdt,
            request.TransactionDate,
            request.ReferenceId,
            request.Notes,
            glTx.Id
        );

        _investorRepository.Update(investor);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new InvestorTransactionDto(
            recordedTx.Id,
            recordedTx.InvestorId,
            recordedTx.FarmId,
            recordedTx.Type.ToString(),
            recordedTx.AmountBdt,
            recordedTx.TransactionDate,
            recordedTx.ReferenceId,
            recordedTx.Notes,
            recordedTx.FinancialTransactionId,
            recordedTx.CreatedAtUtc
        );
    }
}
