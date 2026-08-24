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
        
        // If there's no ledger yet, assume 0 cost.
        decimal totalCost = ledger?.TotalCostBdt ?? 0m;
        
        // Get the latest weight record, or default to 0
        var currentWeight = animal.WeightRecords.OrderByDescending(w => w.RecordedDate).FirstOrDefault()?.Weight.WeightKg ?? 0m;

        // Use the ledger's domain method if ledger exists, otherwise calculate directly
        decimal breakEvenPrice = ledger != null 
            ? ledger.GetBreakEvenPricePerKg(currentWeight)
            : (currentWeight > 0 ? Math.Round(totalCost / currentWeight, 2) : 0m);

        return new BreakEvenCalculatorDto(
            request.AnimalId,
            animal.FarmId,
            currentWeight,
            totalCost,
            breakEvenPrice
        );
    }
}
