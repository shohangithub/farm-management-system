using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Exceptions;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance;
using MediatR;

namespace Farm360.Application.Finance.Queries;

public record GetAnimalCostLedgerQuery(Guid AnimalId) : IRequest<AnimalCostLedgerDto>;

public class GetAnimalCostLedgerQueryHandler : IRequestHandler<GetAnimalCostLedgerQuery, AnimalCostLedgerDto>
{
    private readonly IAnimalCostLedgerRepository _repository;

    public GetAnimalCostLedgerQueryHandler(IAnimalCostLedgerRepository repository)
    {
        _repository = repository;
    }

    public async Task<AnimalCostLedgerDto> Handle(GetAnimalCostLedgerQuery request, CancellationToken cancellationToken)
    {
        var ledger = await _repository.GetByAnimalIdAsync(request.AnimalId, cancellationToken)
            ?? throw new NotFoundException(nameof(AnimalCostLedger), request.AnimalId);

        return new AnimalCostLedgerDto(
            ledger.AnimalId,
            ledger.FarmId,
            ledger.AcquisitionCostBdt,
            ledger.TotalFeedCostBdt,
            ledger.TotalVetCostBdt,
            ledger.TotalLaborCostBdt,
            ledger.TotalOverheadBdt,
            ledger.TotalCostBdt,
            ledger.SaleRevenueBdt,
            ledger.ProfitLossBdt
        );
    }
}
