using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Health;
using Farm360.Domain.Health.Interfaces.Repositories;
using Farm360.Domain.Livestock.Enums;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Health.Commands.VaccinationProtocols;

public sealed record CreateVaccinationProtocolCommand(
    string Title,
    AnimalSpecies TargetSpecies,
    string? Description,
    bool IsDeworming,
    IReadOnlyList<CreateVaccinationProtocolStepDto> Steps
) : IRequest<Guid>;

public sealed record CreateVaccinationProtocolStepDto(
    string StepName,
    int TargetAgeDays,
    string VaccineName,
    string DosageInstruction,
    Guid? InventoryItemId,
    decimal? DosageQuantity
);

public sealed class CreateVaccinationProtocolCommandValidator : AbstractValidator<CreateVaccinationProtocolCommand>
{
    public CreateVaccinationProtocolCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TargetSpecies).IsInEnum();
        RuleFor(x => x.Steps).NotEmpty().WithMessage("At least one protocol step is required.");

        RuleForEach(x => x.Steps).ChildRules(step =>
        {
            step.RuleFor(s => s.StepName).NotEmpty().MaximumLength(100);
            step.RuleFor(s => s.TargetAgeDays).GreaterThanOrEqualTo(0);
            step.RuleFor(s => s.VaccineName).NotEmpty().MaximumLength(100);
        });
    }
}

internal sealed class CreateVaccinationProtocolCommandHandler : IRequestHandler<CreateVaccinationProtocolCommand, Guid>
{
    private readonly IVaccinationRepository _repository;
    private readonly ITenantService _tenantService;
    private readonly IUnitOfWork _unitOfWork;

    public CreateVaccinationProtocolCommandHandler(
        IVaccinationRepository repository,
        ITenantService tenantService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _tenantService = tenantService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreateVaccinationProtocolCommand request, CancellationToken cancellationToken)
    {
        var protocol = VaccinationProtocol.Create(
            _tenantService.TenantId,
            request.Title,
            request.TargetSpecies,
            request.Description,
            request.IsDeworming);

        foreach (var step in request.Steps)
        {
            protocol.AddStep(
                step.StepName,
                step.TargetAgeDays,
                step.VaccineName,
                step.DosageInstruction,
                step.InventoryItemId,
                step.DosageQuantity);
        }

        _repository.AddProtocol(protocol);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return protocol.Id;
    }
}
