using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance.Interfaces;
using MediatR;

namespace Farm360.Application.Finance.Queries;

public record GetInvestorsQuery(Guid FarmId, bool IncludeInactive = false) : IRequest<IReadOnlyList<InvestorDto>>;

public class GetInvestorsQueryHandler : IRequestHandler<GetInvestorsQuery, IReadOnlyList<InvestorDto>>
{
    private readonly IInvestorRepository _repository;

    public GetInvestorsQueryHandler(IInvestorRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<InvestorDto>> Handle(GetInvestorsQuery request, CancellationToken cancellationToken)
    {
        var investors = await _repository.GetByFarmIdAsync(request.FarmId, includeTransactions: true, cancellationToken: cancellationToken);

        if (!request.IncludeInactive)
        {
            investors = investors.Where(i => i.IsActive).ToList();
        }

        var totalActiveCapital = investors.Where(i => i.IsActive).Sum(i => i.CurrentCapitalBdt);

        return investors.Select(i =>
        {
            decimal effectiveShare = i.AgreedProfitSharePercentage
                ?? (totalActiveCapital > 0 ? Math.Round((i.CurrentCapitalBdt / totalActiveCapital) * 100m, 2) : 0m);

            return new InvestorDto(
                i.Id,
                i.FarmId,
                i.Name,
                i.Email,
                i.Phone,
                i.NationalId,
                i.InvestmentDate,
                i.TotalInvestedBdt,
                i.TotalWithdrawnBdt,
                i.CurrentCapitalBdt,
                i.TotalProfitPaidBdt,
                i.AgreedProfitSharePercentage,
                effectiveShare,
                i.Notes,
                i.IsActive,
                i.CreatedAtUtc,
                i.Transactions.OrderByDescending(t => t.TransactionDate).Select(t => new InvestorTransactionDto(
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
        }).ToList();
    }
}
