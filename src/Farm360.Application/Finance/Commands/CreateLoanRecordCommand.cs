using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
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

    public CreateLoanRecordCommandHandler(ILoanRecordRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
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
