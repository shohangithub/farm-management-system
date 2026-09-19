using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance;
using MediatR;

namespace Farm360.Application.Finance.Queries;

public record GetAnimalCostLedgerQuery(Guid AnimalId) : IRequest<AnimalCostLedgerDto>;

public class GetAnimalCostLedgerQueryHandler : IRequestHandler<GetAnimalCostLedgerQuery, AnimalCostLedgerDto>
{
    private readonly IAnimalCostLedgerRepository _repository;
    private readonly IAnimalFeedAllocationRepository _feedAllocationRepository;
    private readonly ITenantService _tenantService;
    private readonly IUnitOfWork _unitOfWork;

    public GetAnimalCostLedgerQueryHandler(
        IAnimalCostLedgerRepository repository,
        IAnimalFeedAllocationRepository feedAllocationRepository,
        ITenantService tenantService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _feedAllocationRepository = feedAllocationRepository;
        _tenantService = tenantService;
        _unitOfWork = unitOfWork;
    }

    public async Task<AnimalCostLedgerDto> Handle(GetAnimalCostLedgerQuery request, CancellationToken cancellationToken)
    {
        var ledger = await _repository.GetByAnimalIdAsync(request.AnimalId, cancellationToken)
            ?? throw new NotFoundException(nameof(AnimalCostLedger), request.AnimalId);

        // Feed cost now comes from AnimalFeedAllocations (docs/32 GAP-1), which carry a row for
        // every animal — including those fed under a batch, shed or pen plan. The previous source
        // walked DailyFeedingEntry via the plan's nullable AnimalId, so a group-fed animal matched
        // nothing and silently reported zero feed cost for its whole life.
        var feedTotals = await _feedAllocationRepository.GetTotalsForAnimalAsync(
            request.AnimalId,
            DateOnly.MinValue,
            DateOnly.MaxValue,
            cancellationToken);

        if (feedTotals.TotalCostBdt != ledger.TotalFeedCostBdt)
        {
            ledger.UpdateFeedCost(feedTotals.TotalCostBdt);
            _repository.Update(ledger);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

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
