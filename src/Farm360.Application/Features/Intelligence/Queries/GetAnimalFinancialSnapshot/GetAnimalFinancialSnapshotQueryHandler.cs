using Farm360.Domain.Interfaces.Repositories;
using Farm360.Domain.Livestock.Repositories;
using MediatR;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace Farm360.Application.Features.Intelligence.Queries.GetAnimalFinancialSnapshot;

public sealed class GetAnimalFinancialSnapshotQueryHandler : IRequestHandler<GetAnimalFinancialSnapshotQuery, AnimalFinancialSnapshotDto>
{
    private readonly IAnimalRepository _animalRepository;
    // Inject other repos like IFinanceRepository or IFeedRepository when they exist.

    public GetAnimalFinancialSnapshotQueryHandler(IAnimalRepository animalRepository)
    {
        _animalRepository = animalRepository;
    }

    public async Task<AnimalFinancialSnapshotDto> Handle(GetAnimalFinancialSnapshotQuery request, CancellationToken cancellationToken)
    {
        var animal = await _animalRepository.GetByIdAsync(request.AnimalId, cancellationToken)
            ?? throw new Farm360.Application.Common.Exceptions.NotFoundException(nameof(Farm360.Domain.Livestock.Animal), request.AnimalId);

        // STUB logic for Phase 1 Cost & Profit Engine calculation.
        // In reality, this will sum up actual FeedConsumptionDetails + MedicalTreatments + Acquisition Price.
        
        decimal initialCost = animal.AcquisitionPriceBdt ?? 0;
        
        // Mock calculations for the demo. Real engine would read from the DB.
        decimal totalInvestment = initialCost + 5000m; // Add mock feed/health cost
        
        decimal projected30DayCost = 4500m;
        decimal projected60DayCost = 9000m;
        
        decimal estimatedMarketValue = (animal.LatestWeightKg ?? 0) * 450m; // 450 BDT per Kg live weight mock
        if (estimatedMarketValue == 0) estimatedMarketValue = initialCost * 1.5m; // Fallback
        
        decimal currentProfitMargin = estimatedMarketValue - totalInvestment;

        return new AnimalFinancialSnapshotDto(
            request.AnimalId,
            totalInvestment,
            projected30DayCost,
            projected60DayCost,
            estimatedMarketValue,
            currentProfitMargin);
    }
}
