using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Health;
using Farm360.Domain.Health.Enums;
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
    IReadOnlyList<UpdateVaccinationProtocolStepDto> Steps,
    // Null leaves the setting as it is.
    bool? AutoStartForPurchased = null
) : IRequest;

public sealed record UpdateVaccinationProtocolStepDto(
    string StepName,
    int TargetAgeDays,
    string VaccineName,
    string DosageInstruction,
    // The stock item and dose were dropped on every edit before; they are kept now.
    Guid? InventoryItemId = null,
    decimal? DosageQuantity = null,
    HealthTaskKind Kind = HealthTaskKind.Medicine,
    int? RepeatEveryDays = null,
    decimal? DoseAmount = null,
    decimal? DosePerKg = null,
    DoseRounding DoseRounding = DoseRounding.None,
    decimal? DoseMin = null,
    decimal? DoseMax = null,
    string? DoseUnit = null,
    Guid? DoseUnitId = null,
    string? ActionLink = null
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
            step.RuleFor(s => s.Kind).IsInEnum();
            step.RuleFor(s => s.DoseRounding).IsInEnum();
            step.RuleFor(s => s.RepeatEveryDays).InclusiveBetween(1, 3650).When(s => s.RepeatEveryDays.HasValue)
                .WithMessage("Repeat every must be at least 1 day.");
            step.RuleFor(s => s.DosePerKg).GreaterThan(0).When(s => s.DoseAmount.HasValue)
                .WithMessage("Give the body weight (kg) the dose is for.");
            step.RuleFor(s => s.DoseAmount).GreaterThan(0).When(s => s.DosePerKg.HasValue)
                .WithMessage("Give the dose for that body weight.");
            step.RuleFor(s => s.DoseMin).GreaterThanOrEqualTo(0).When(s => s.DoseMin.HasValue);
            step.RuleFor(s => s.DoseMax).GreaterThanOrEqualTo(s => s.DoseMin ?? 0).When(s => s.DoseMax.HasValue)
                .WithMessage("The maximum dose cannot be less than the minimum dose.");
            step.RuleFor(s => s.DoseUnit).MaximumLength(30);
            step.RuleFor(s => s.ActionLink).MaximumLength(50);
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
        if (request.AutoStartForPurchased is { } autoStart)
            protocol.SetAutoStartForPurchased(autoStart);
        
        protocol.ClearSteps();

        foreach (var step in request.Steps)
        {
            protocol.AddStep(
                step.StepName,
                step.TargetAgeDays,
                step.VaccineName,
                step.DosageInstruction,
                step.Kind == HealthTaskKind.CareTask ? null : step.InventoryItemId,
                step.Kind == HealthTaskKind.CareTask ? null : step.DosageQuantity,
                new ProtocolStepOptions(
                    step.Kind,
                    step.RepeatEveryDays,
                    WeightDoseRule.From(step.DoseAmount, step.DosePerKg, step.DoseRounding, step.DoseMin, step.DoseMax, step.DoseUnit),
                    step.DoseUnitId,
                    step.ActionLink));
        }

        _repository.UpdateProtocol(protocol);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
