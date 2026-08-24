using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Contracts.Finance;
using MediatR;

namespace Farm360.Application.Finance.Queries;

public record GetLoansQuery(Guid FarmId) : IRequest<IReadOnlyList<LoanRecordDto>>;

public class GetLoansQueryHandler : IRequestHandler<GetLoansQuery, IReadOnlyList<LoanRecordDto>>
{
    private readonly ILoanRecordRepository _repository;

    public GetLoansQueryHandler(ILoanRecordRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<LoanRecordDto>> Handle(GetLoansQuery request, CancellationToken cancellationToken)
    {
        var loans = await _repository.GetByFarmIdAsync(request.FarmId, cancellationToken);
        
        return loans.Select(loan => new LoanRecordDto(
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
        )).ToList();
    }
}
