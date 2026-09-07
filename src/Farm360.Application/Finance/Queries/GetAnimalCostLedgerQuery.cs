using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Feeding.Enums;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance;
using MediatR;

namespace Farm360.Application.Finance.Queries;

public record GetAnimalCostLedgerQuery(Guid AnimalId) : IRequest<AnimalCostLedgerDto>;

public class GetAnimalCostLedgerQueryHandler : IRequestHandler<GetAnimalCostLedgerQuery, AnimalCostLedgerDto>
{
    private readonly IAnimalCostLedgerRepository _repository;
    private readonly IDailyFeedingEntryRepository _feedingEntryRepository;
    private readonly ITenantService _tenantService;
    private readonly IUnitOfWork _unitOfWork;

    public GetAnimalCostLedgerQueryHandler(
        IAnimalCostLedgerRepository repository,
        IDailyFeedingEntryRepository feedingEntryRepository,
        ITenantService tenantService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _feedingEntryRepository = feedingEntryRepository;
        _tenantService = tenantService;
        _unitOfWork = unitOfWork;
    }

    public async Task<AnimalCostLedgerDto> Handle(GetAnimalCostLedgerQuery request, CancellationToken cancellationToken)
    {
        var ledger = await _repository.GetByAnimalIdAsync(request.AnimalId, cancellationToken)
            ?? throw new NotFoundException(nameof(AnimalCostLedger), request.AnimalId);

        // Sync feed cost if ledger feed cost is currently 0
        if (ledger.TotalFeedCostBdt == 0)
        {
            try
            {
                var entries = await _feedingEntryRepository.GetEntriesByAnimalIdAsync(_tenantService.TenantId, request.AnimalId, cancellationToken);
                var totalCost = entries
                    .Where(e => (e.Status == DailyFeedingEntryStatus.Confirmed || e.Status == DailyFeedingEntryStatus.Adjusted) && e.TotalCostBdt.HasValue)
                    .Sum(e => e.TotalCostBdt!.Value);

                if (totalCost > 0)
                {
                    ledger.UpdateFeedCost(totalCost);
                    _repository.Update(ledger);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }
            }
#pragma warning disable CA1031
            catch
            {
                // Non-blocking fallback
            }
#pragma warning restore CA1031
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
