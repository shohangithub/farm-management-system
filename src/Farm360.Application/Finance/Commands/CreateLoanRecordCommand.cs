using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Application.Finance.Services;
using MediatR;

namespace Farm360.Application.Finance.Commands;

public record CreateLoanRecordCommand(
    Guid TenantId,
    Guid FarmId,
    string LenderName,
    decimal PrincipalAmountBdt,
    decimal InterestRatePercent,
    DateTime DisbursementDate,
    string Schedule,
    string? Notes
) : IRequest<LoanRecordDto>;

public class CreateLoanRecordCommandHandler : IRequestHandler<CreateLoanRecordCommand, LoanRecordDto>
{
    private readonly ILoanRecordRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILedgerPostingService _ledgerPosting;

    public CreateLoanRecordCommandHandler(ILoanRecordRepository repository, IUnitOfWork unitOfWork, ILedgerPostingService ledgerPosting)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _ledgerPosting = ledgerPosting;
    }

    public async Task<LoanRecordDto> Handle(CreateLoanRecordCommand request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<RepaymentSchedule>(request.Schedule, true, out var schedule))
            throw new ArgumentException($"Invalid repayment schedule: {request.Schedule}");

        var loan = LoanRecord.Create(
            request.TenantId,
            request.FarmId,
            request.LenderName,
            request.PrincipalAmountBdt,
            request.InterestRatePercent,
            request.DisbursementDate,
            schedule,
            request.Notes
        );

        _repository.Add(loan);

        // The borrowed money arrives in the farm: Dr Cash, Cr Loans Payable. Without this the cash
        // book never saw the loan while every repayment still reduced cash.
        await _ledgerPosting.PostAsync(new LedgerPosting(
            request.TenantId,
            request.FarmId,
            TransactionCategory.LoanDisbursement,
            loan.PrincipalAmountBdt,
            loan.DisbursementDate,
            loan.Id,
            "Finance",
            $"Loan received from {loan.LenderName}",
            Notes: $"{loan.InterestRatePercent:0.##}% flat, {loan.Schedule} repayment"), cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return loan.ToDto();
    }
}
