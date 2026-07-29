using Farm360.Application.Intelligence.Interfaces;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Application.Intelligence.Queries;

public sealed record SimulateSaleQuery(Guid AnimalId, DateOnly TargetDate) : IRequest<SaleSimulationResult?>;

public class SimulateSaleQueryHandler : IRequestHandler<SimulateSaleQuery, SaleSimulationResult?>
{
    private readonly ISimulationEngine _simulationEngine;

    public SimulateSaleQueryHandler(ISimulationEngine simulationEngine)
    {
        _simulationEngine = simulationEngine;
    }

    public async Task<SaleSimulationResult?> Handle(SimulateSaleQuery request, CancellationToken cancellationToken)
    {
        return await _simulationEngine.SimulateSaleAsync(request.AnimalId, request.TargetDate, cancellationToken);
    }
}
