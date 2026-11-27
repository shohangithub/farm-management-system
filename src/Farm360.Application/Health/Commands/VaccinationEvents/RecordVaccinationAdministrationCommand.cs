using Farm360.Application.Common.Behaviors;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Health.Tasks;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Health.Commands.VaccinationEvents;

public sealed record RecordVaccinationAdministrationCommand(
    Guid VaccinationEventId,
    DateOnly AdministeredDate,
    string? Notes
) : IRequest, ITransactionalCommand;

public sealed class RecordVaccinationAdministrationCommandValidator : AbstractValidator<RecordVaccinationAdministrationCommand>
{
    public RecordVaccinationAdministrationCommandValidator(IFarmClock? clock = null)
    {
        clock ??= Farm360.Application.Common.Time.FarmClock.System;
        RuleFor(v => v.VaccinationEventId).NotEmpty();
        RuleFor(v => v.Notes).MaximumLength(1000);
        RuleFor(v => v.AdministeredDate)
            .LessThanOrEqualTo(_ => clock.Today)
            .WithMessage("Administered date cannot be in the future.");
    }
}

/// <summary>
/// Records a dose as given. Same path as "Today's tasks": the animal must not have left before the
/// date, a weight-based dose is worked out from the latest weight, the stock item is deducted (via
/// <c>VaccinationAdministeredEvent</c>) and a repeating step's next dose is scheduled from the actual date.
/// </summary>
internal sealed class RecordVaccinationAdministrationCommandHandler(IHealthTaskCompletionService completion)
    : IRequestHandler<RecordVaccinationAdministrationCommand>
{
    public Task Handle(RecordVaccinationAdministrationCommand request, CancellationToken cancellationToken) =>
        completion.CompleteAsync([request.VaccinationEventId], request.AdministeredDate, request.Notes, cancellationToken);
}
