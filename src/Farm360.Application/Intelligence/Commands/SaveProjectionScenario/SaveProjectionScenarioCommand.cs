using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Intelligence.Projections;
using Farm360.Domain.Intelligence.Repositories;
using FluentValidation;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Application.Intelligence.Commands.SaveProjectionScenario;

public record SaveProjectionScenarioCommand(
    Guid? AnimalId,
    string Name,
    string Description,
    FatteningProjectionInputs Inputs) : IRequest<Guid>;

public class SaveProjectionScenarioValidator : AbstractValidator<SaveProjectionScenarioCommand>
{
    public SaveProjectionScenarioValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Inputs).NotNull();
    }
}

internal sealed class SaveProjectionScenarioCommandHandler : IRequestHandler<SaveProjectionScenarioCommand, Guid>
{
    private readonly IProjectionScenarioRepository _scenarioRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITenantService _tenantService;

    public SaveProjectionScenarioCommandHandler(
        IProjectionScenarioRepository scenarioRepository,
        IUnitOfWork unitOfWork,
        ITenantService tenantService)
    {
        _scenarioRepository = scenarioRepository;
        _unitOfWork = unitOfWork;
        _tenantService = tenantService;
    }

    public async Task<Guid> Handle(SaveProjectionScenarioCommand request, CancellationToken cancellationToken)
    {
        var scenario = new ProjectionScenario(
            Guid.NewGuid(),
            _tenantService.TenantId,
            request.AnimalId,
            request.Name,
            request.Description,
            request.Inputs);

        _scenarioRepository.Add(scenario);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return scenario.Id;
    }
}
