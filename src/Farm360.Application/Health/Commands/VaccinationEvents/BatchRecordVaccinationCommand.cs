using Farm360.Application.Common.Behaviors;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Health;
using Farm360.Domain.Health.Interfaces.Repositories;
using Farm360.Domain.Livestock.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Health.Commands.VaccinationEvents;

public sealed record BatchRecordVaccinationCommand(
    IReadOnlyList<Guid> AnimalIds,
    string VaccineName,
    string BatchNumber,
    DateOnly AdministeredDate,
    string? Notes
) : IRequest, ITransactionalCommand;

public sealed class BatchRecordVaccinationCommandValidator : AbstractValidator<BatchRecordVaccinationCommand>
{
    public BatchRecordVaccinationCommandValidator()
    {
        RuleFor(v => v.AnimalIds)
            .NotEmpty().WithMessage("At least one animal must be selected.")
            .Must(ids => ids.Count <= 500).WithMessage("Cannot batch vaccinate more than 500 animals at once.");
            
        RuleFor(v => v.VaccineName).NotEmpty().MaximumLength(100);
        RuleFor(v => v.BatchNumber).MaximumLength(50);
        RuleFor(v => v.Notes).MaximumLength(1000);
        
        RuleFor(v => v.AdministeredDate)
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Administered date cannot be in the future.");
    }
}

internal sealed class BatchRecordVaccinationCommandHandler(
    IVaccinationRepository vaccinationRepository,
    IAnimalRepository animalRepository,
    ITenantService tenantService,
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork) : IRequestHandler<BatchRecordVaccinationCommand>
{
    public async Task Handle(BatchRecordVaccinationCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantService.TenantId;
        var userId = currentUserService.UserId ?? Guid.Empty;

        // Fetch all requested animals
        var animals = await animalRepository.GetByIdsAsync(request.AnimalIds, cancellationToken);
        var validAnimalIds = animals.Select(a => a.Id).ToHashSet();

        foreach (var animalId in request.AnimalIds)
        {
            if (!validAnimalIds.Contains(animalId))
            {
                // Skip invalid animals that do not belong to the tenant
                continue;
            }

            // Create scheduled event first
            var @event = VaccinationEvent.Schedule(
                tenantId,
                animalId,
                null,
                request.VaccineName,
                request.BatchNumber,
                request.AdministeredDate, // Schedule and administer on the same date
                request.Notes);

            // Immediately mark it as administered
            @event.RecordAdministration(request.AdministeredDate, userId, request.Notes);

            vaccinationRepository.AddEvent(@event);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
