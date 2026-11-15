using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Exceptions;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Livestock.Repositories;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance;
using Farm360.Domain.Livestock;
using MediatR;

namespace Farm360.Application.Finance.Queries;

public record GetBreakEvenCalculatorQuery(Guid AnimalId) : IRequest<BreakEvenCalculatorDto>;

public class GetBreakEvenCalculatorQueryHandler : IRequestHandler<GetBreakEvenCalculatorQuery, BreakEvenCalculatorDto>
{
    private readonly IAnimalCostLedgerRepository _ledgerRepository;
    private readonly IAnimalRepository _animalRepository;

    public GetBreakEvenCalculatorQueryHandler(
        IAnimalCostLedgerRepository ledgerRepository,
        IAnimalRepository animalRepository)
    {
        _ledgerRepository = ledgerRepository;
        _animalRepository = animalRepository;
    }

    public async Task<BreakEvenCalculatorDto> Handle(GetBreakEvenCalculatorQuery request, CancellationToken cancellationToken)
    {
        var animal = await _animalRepository.GetByIdAsync(request.AnimalId, cancellationToken)
            ?? throw new NotFoundException(nameof(Animal), request.AnimalId);

        var ledger = await _ledgerRepository.GetByAnimalIdAsync(request.AnimalId, cancellationToken);

        // The same total the cost-ledger screen shows (its feed bucket is kept in step with the
        // confirmed feed allocations). Without a ledger yet, the purchase price is the cost.
        decimal totalCost = ledger?.TotalCostBdt ?? (animal.AcquisitionPriceBdt ?? 0m);

        // Break-even is per kg of LIVE weight: the latest recorded weight.
        var currentWeight = animal.LatestWeightKg
            ?? animal.WeightRecords.OrderByDescending(w => w.RecordedDate).FirstOrDefault()?.Weight.WeightKg
            ?? 0m;

        decimal PerLiveKg(decimal amount) => currentWeight > 0 ? Math.Round(amount / currentWeight, 2) : 0m;

        var breakEvenPrice = PerLiveKg(totalCost);
        var price10 = PerLiveKg(totalCost * 1.10m);
        var price20 = PerLiveKg(totalCost * 1.20m);
        var price30 = PerLiveKg(totalCost * 1.30m);

        return new BreakEvenCalculatorDto(
            request.AnimalId,
            animal.FarmId,
            currentWeight,
            totalCost,
            breakEvenPrice,
            animal.Tag?.TagId,
            price10,
            price20,
            price30
        );
    }
}
