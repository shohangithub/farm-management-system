using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Health;
using Farm360.Domain.Health.Interfaces.Repositories;
using Farm360.Domain.Livestock.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Health.Commands.VaccinationEvents;

public sealed record ScheduleVaccinationCommand(
    Guid AnimalId,
    Guid? ProtocolStepId,
    string VaccineName,
    string BatchNumber,
    DateOnly ScheduledDate,
    string? Notes
) : IRequest<Guid>;

public sealed class ScheduleVaccinationCommandValidator : AbstractValidator<ScheduleVaccinationCommand>
{
    public ScheduleVaccinationCommandValidator()
    {
        RuleFor(v => v.AnimalId).NotEmpty();
        RuleFor(v => v.VaccineName).NotEmpty().MaximumLength(100);
        RuleFor(v => v.BatchNumber).MaximumLength(50);
        RuleFor(v => v.Notes).MaximumLength(1000);
        
        // Cannot schedule vaccination for a date older than today. 
        // We only schedule forward.
        RuleFor(v => v.ScheduledDate)
            .GreaterThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Scheduled date must be today or in the future.");
    }
}

internal sealed class ScheduleVaccinationCommandHandler(
    IVaccinationRepository vaccinationRepository,
    IAnimalRepository animalRepository,
    ITenantService tenantService,
    IUnitOfWork unitOfWork) : IRequestHandler<ScheduleVaccinationCommand, Guid>
{
    public async Task<Guid> Handle(ScheduleVaccinationCommand request, CancellationToken cancellationToken)
    {
        // Validate animal exists and belongs to tenant
        var animal = await animalRepository.GetByIdAsync(request.AnimalId, cancellationToken)
            ?? throw new KeyNotFoundException($"Animal with ID '{request.AnimalId}' was not found.");

        var @event = VaccinationEvent.Schedule(
            tenantService.TenantId,
            request.AnimalId,
            request.ProtocolStepId,
            request.VaccineName,
            request.BatchNumber,
            request.ScheduledDate,
            request.Notes);

        vaccinationRepository.AddEvent(@event);
        
        await using var tx = await unitOfWork.BeginTransactionAsync(cancellationToken);
        await unitOfWork.CommitTransactionAsync(tx, cancellationToken);

        return @event.Id;
    }
}
