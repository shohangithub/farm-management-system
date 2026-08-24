using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Repositories;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using MediatR;

namespace Farm360.Application.Finance.Commands;

public record RecordLoanRepaymentCommand(
    Guid TenantId,
    Guid FarmId,
    Guid LoanId,
    decimal AmountBdt,
    DateTime RepaymentDate,
    string ReferenceId,
    string Notes
) : IRequest<LoanRecordDto>;

public class RecordLoanRepaymentCommandHandler : IRequestHandler<RecordLoanRepaymentCommand, LoanRecordDto>
{
    private readonly ILoanRecordRepository _loanRepository;
    private readonly IFinancialTransactionRepository _transactionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RecordLoanRepaymentCommandHandler(
        ILoanRecordRepository loanRepository,
        IFinancialTransactionRepository transactionRepository,
        IUnitOfWork unitOfWork)
    {
        _loanRepository = loanRepository;
        _transactionRepository = transactionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<LoanRecordDto> Handle(RecordLoanRepaymentCommand request, CancellationToken cancellationToken)
    {
        var loan = await _loanRepository.GetByIdAsync(request.LoanId, cancellationToken);
        if (loan == null || loan.TenantId != request.TenantId)
            throw new NotFoundException(nameof(LoanRecord), request.LoanId);

        // 1. Update the loan aggregate
        loan.RecordRepayment(request.AmountBdt);
        _loanRepository.Update(loan);

        // 2. Automatically record a financial transaction (Expense for repayment)
        var transaction = FinancialTransaction.Create(
            request.TenantId,
            request.FarmId,
            TransactionType.Expense,
            TransactionCategory.LoanRepayment,
            request.AmountBdt,
            request.RepaymentDate,
            request.ReferenceId,
            request.Notes,
            $"Repayment for loan from {loan.LenderName}"
        );
        await _transactionRepository.AddAsync(transaction, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new LoanRecordDto(
            loan.Id,
            loan.FarmId,
            loan.LenderName,
            loan.PrincipalAmountBdt,
            loan.InterestRatePercent,
            loan.DisbursementDate,
            loan.Schedule.ToString(),
            loan.TotalRepaidBdt,
            loan.OutstandingBalanceBdt,
            loan.RepaymentProgressPercent,
            loan.Notes,
            loan.IsActive,
            loan.CreatedAtUtc
        );
    }
}
