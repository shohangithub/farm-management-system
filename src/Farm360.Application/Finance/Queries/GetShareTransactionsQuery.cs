using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance.Interfaces;
using MediatR;

namespace Farm360.Application.Finance.Queries;

public record GetShareTransactionsQuery(
    Guid FarmId,
    Guid? InvestorId = null
) : IRequest<IReadOnlyList<ShareTransactionDto>>;

public class GetShareTransactionsQueryHandler : IRequestHandler<GetShareTransactionsQuery, IReadOnlyList<ShareTransactionDto>>
{
    private readonly IFarmShareRepository _shareRepository;
    private readonly IInvestorRepository _investorRepository;

    public GetShareTransactionsQueryHandler(
        IFarmShareRepository shareRepository,
        IInvestorRepository investorRepository)
    {
        _shareRepository = shareRepository;
        _investorRepository = investorRepository;
    }

    public async Task<IReadOnlyList<ShareTransactionDto>> Handle(GetShareTransactionsQuery request, CancellationToken cancellationToken)
    {
        var rawTxs = request.InvestorId.HasValue
            ? await _shareRepository.GetTransactionsByInvestorIdAsync(request.InvestorId.Value, cancellationToken)
            : await _shareRepository.GetTransactionsByFarmIdAsync(request.FarmId, cancellationToken);

        var investors = await _investorRepository.GetByFarmIdAsync(request.FarmId, false, cancellationToken);
        var investorMap = investors.ToDictionary(i => i.Id);

        return rawTxs.Select(t =>
        {
            investorMap.TryGetValue(t.InvestorId, out var inv);
            string? cpName = null;
            if (t.CounterpartyInvestorId.HasValue && investorMap.TryGetValue(t.CounterpartyInvestorId.Value, out var cp))
            {
                cpName = cp.Name;
            }

            return new ShareTransactionDto(
                t.Id,
                t.FarmId,
                t.InvestorId,
                inv?.Name ?? "Unknown Investor",
                t.Type.ToString(),
                t.ShareCount,
                t.PricePerShareBdt,
                t.TotalAmountBdt,
                t.TransactionDate,
                t.CounterpartyInvestorId,
                cpName,
                t.ReferenceId,
                t.Notes,
                t.FinancialTransactionId,
                t.CreatedAtUtc
            );
        }).ToList();
    }
}
