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
using Farm360.Application.Finance.Services;
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
    private readonly ILedgerPostingService _ledgerPosting;
    private readonly IUnitOfWork _unitOfWork;

    public RecordLoanRepaymentCommandHandler(
        ILoanRecordRepository loanRepository,
        ILedgerPostingService ledgerPosting,
        IUnitOfWork unitOfWork)
    {
        _loanRepository = loanRepository;
        _ledgerPosting = ledgerPosting;
        _unitOfWork = unitOfWork;
    }

    public async Task<LoanRecordDto> Handle(RecordLoanRepaymentCommand request, CancellationToken cancellationToken)
    {
        var loan = await _loanRepository.GetByIdAsync(request.LoanId, cancellationToken);
        if (loan == null || loan.TenantId != request.TenantId)
            throw new NotFoundException(nameof(LoanRecord), request.LoanId);

        // 1. Update the loan aggregate and split the payment into principal and interest.
        var (principal, interest) = loan.RecordRepayment(request.AmountBdt);
        _loanRepository.Update(loan);

        // 2. Book both parts. Principal reduces the debt (Dr Loans Payable); interest is a cost of
        //    the period (Dr Interest Expense). Booking the whole payment as debt reduction made
        //    interest vanish from profit and overstated how much of the loan was paid off.
        var repaymentId = Guid.NewGuid();
        var description = $"Repayment for loan from {loan.LenderName}";

        await _ledgerPosting.PostAsync(new LedgerPosting(
            request.TenantId, request.FarmId, TransactionCategory.LoanRepayment,
            principal, request.RepaymentDate, repaymentId, "Finance", description,
            Notes: string.IsNullOrWhiteSpace(request.Notes) ? $"Principal portion of {request.AmountBdt:N2} BDT" : request.Notes,
            ReferenceId: request.ReferenceId ?? string.Empty), cancellationToken);

        await _ledgerPosting.PostAsync(new LedgerPosting(
            request.TenantId, request.FarmId, TransactionCategory.InterestExpense,
            interest, request.RepaymentDate, repaymentId, "Finance", $"Interest on loan from {loan.LenderName}",
            Notes: $"Interest portion of {request.AmountBdt:N2} BDT",
            ReferenceId: request.ReferenceId ?? string.Empty), cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return loan.ToDto();
    }
}
