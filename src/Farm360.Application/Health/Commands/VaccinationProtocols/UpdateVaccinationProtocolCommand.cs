using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Health;
using Farm360.Domain.Health.Interfaces.Repositories;
using Farm360.Domain.Livestock.Enums;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Health.Commands.VaccinationProtocols;

public sealed record UpdateVaccinationProtocolCommand(
    Guid Id,
    string Title,
    AnimalSpecies TargetSpecies,
    string? Description,
    IReadOnlyList<UpdateVaccinationProtocolStepDto> Steps
) : IRequest;

public sealed record UpdateVaccinationProtocolStepDto(
    string StepName,
    int TargetAgeDays,
    string VaccineName,
    string DosageInstruction
);

public sealed class UpdateVaccinationProtocolCommandValidator : AbstractValidator<UpdateVaccinationProtocolCommand>
{
    public UpdateVaccinationProtocolCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
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

internal sealed class UpdateVaccinationProtocolCommandHandler : IRequestHandler<UpdateVaccinationProtocolCommand>
{
    private readonly IVaccinationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateVaccinationProtocolCommandHandler(
        IVaccinationRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdateVaccinationProtocolCommand request, CancellationToken cancellationToken)
    {
        var protocol = await _repository.GetProtocolByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(VaccinationProtocol), request.Id);

        protocol.Update(request.Title, request.TargetSpecies, request.Description);
        
        protocol.ClearSteps();

        foreach (var step in request.Steps)
        {
            protocol.AddStep(
                step.StepName,
                step.TargetAgeDays,
                step.VaccineName,
                step.DosageInstruction);
        }

        _repository.UpdateProtocol(protocol);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
