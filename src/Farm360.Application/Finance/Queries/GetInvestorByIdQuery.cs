using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Exceptions;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Interfaces;
using MediatR;

namespace Farm360.Application.Finance.Queries;

public record GetInvestorByIdQuery(Guid Id) : IRequest<InvestorDto>;

public class GetInvestorByIdQueryHandler : IRequestHandler<GetInvestorByIdQuery, InvestorDto>
{
    private readonly IInvestorRepository _repository;

    public GetInvestorByIdQueryHandler(IInvestorRepository repository)
    {
        _repository = repository;
    }

    public async Task<InvestorDto> Handle(GetInvestorByIdQuery request, CancellationToken cancellationToken)
    {
        var investor = await _repository.GetByIdWithTransactionsAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Investor), request.Id);

        var allInvestors = await _repository.GetByFarmIdAsync(investor.FarmId, cancellationToken: cancellationToken);
        var totalActiveCapital = allInvestors.Where(i => i.IsActive).Sum(i => i.CurrentCapitalBdt);

        decimal effectiveShare = investor.AgreedProfitSharePercentage
            ?? (totalActiveCapital > 0 ? Math.Round((investor.CurrentCapitalBdt / totalActiveCapital) * 100m, 2) : 0m);

        return new InvestorDto(
            investor.Id,
            investor.FarmId,
            investor.Name,
            investor.Email,
            investor.Phone,
            investor.NationalId,
            investor.InvestmentDate,
            investor.TotalInvestedBdt,
            investor.TotalWithdrawnBdt,
            investor.CurrentCapitalBdt,
            investor.TotalProfitPaidBdt,
            investor.AgreedProfitSharePercentage,
            effectiveShare,
            investor.Notes,
            investor.IsActive,
            investor.CreatedAtUtc,
            investor.Transactions.OrderByDescending(t => t.TransactionDate).Select(t => new InvestorTransactionDto(
                t.Id,
                t.InvestorId,
                t.FarmId,
                t.Type.ToString(),
                t.AmountBdt,
                t.TransactionDate,
                t.ReferenceId,
                t.Notes,
                t.FinancialTransactionId,
                t.CreatedAtUtc
            )).ToList()
        );
    }
}
