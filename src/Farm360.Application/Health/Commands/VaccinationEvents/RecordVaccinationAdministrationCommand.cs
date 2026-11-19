using Farm360.Application.Common.Behaviors;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Health.Interfaces.Repositories;
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

internal sealed class RecordVaccinationAdministrationCommandHandler(
    IVaccinationRepository vaccinationRepository,
    Farm360.Domain.Livestock.Repositories.IAnimalRepository animalRepository,
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork,
    IPublisher publisher,
    IFarmClock? clock = null) : IRequestHandler<RecordVaccinationAdministrationCommand>
{
    private readonly IFarmClock _clock = clock ?? Farm360.Application.Common.Time.FarmClock.System;

    public async Task Handle(RecordVaccinationAdministrationCommand request, CancellationToken cancellationToken)
    {
        var @event = await vaccinationRepository.GetEventByIdAsync(request.VaccinationEventId, cancellationToken)
            ?? throw new KeyNotFoundException($"Vaccination event with ID '{request.VaccinationEventId}' was not found.");

        // A dose given before the animal left may be entered late; none on/after the exit date.
        var animal = await animalRepository.GetByIdAsync(@event.AnimalId, cancellationToken)
            ?? throw new KeyNotFoundException($"The animal for this vaccination ({@event.AnimalId}) was not found.");
        animal.EnsureCanRecordOn(request.AdministeredDate, "a vaccination");

        var userId = currentUserService.UserId ?? Guid.Empty;

        @event.RecordAdministration(request.AdministeredDate, userId, request.Notes, _clock.Today);

        vaccinationRepository.UpdateEvent(@event);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Stock deduction and its cost posting run off this event; it was never published before,
        // so vaccine stock was never reduced and its cost never reached the books.
        foreach (var administered in @event.DomainEvents.OfType<Farm360.Domain.Health.Events.VaccinationAdministeredEvent>().ToList())
        {
            await publisher.Publish(administered, cancellationToken);
        }
    }
}
