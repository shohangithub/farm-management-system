using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Health.Interfaces.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Health.Commands.VaccinationEvents;

public sealed record RecordVaccinationAdministrationCommand(
    Guid VaccinationEventId,
    DateOnly AdministeredDate,
    string? Notes
) : IRequest;

public sealed class RecordVaccinationAdministrationCommandValidator : AbstractValidator<RecordVaccinationAdministrationCommand>
{
    public RecordVaccinationAdministrationCommandValidator()
    {
        RuleFor(v => v.VaccinationEventId).NotEmpty();
        RuleFor(v => v.Notes).MaximumLength(1000);
        RuleFor(v => v.AdministeredDate)
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Administered date cannot be in the future.");
    }
}

internal sealed class RecordVaccinationAdministrationCommandHandler(
    IVaccinationRepository vaccinationRepository,
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork) : IRequestHandler<RecordVaccinationAdministrationCommand>
{
    public async Task Handle(RecordVaccinationAdministrationCommand request, CancellationToken cancellationToken)
    {
        var @event = await vaccinationRepository.GetEventByIdAsync(request.VaccinationEventId, cancellationToken)
            ?? throw new KeyNotFoundException($"Vaccination event with ID '{request.VaccinationEventId}' was not found.");

        var userId = currentUserService.UserId ?? Guid.Empty;

        @event.RecordAdministration(request.AdministeredDate, userId, request.Notes);

        vaccinationRepository.UpdateEvent(@event);
        
        await using var tx = await unitOfWork.BeginTransactionAsync(cancellationToken);
        await unitOfWork.CommitTransactionAsync(tx, cancellationToken);
    }
}
