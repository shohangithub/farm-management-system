using Farm360.Application.Common.Behaviors;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Health;
using Farm360.Domain.Health.Exceptions;
using Farm360.Domain.Health.Interfaces.Repositories;
using Farm360.Domain.Health.ValueObjects;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Health.Commands.MedicalTreatments;

public sealed record LogMedicalTreatmentCommand(
    Guid AnimalId,
    string Diagnosis,
    string MedicationName,
    decimal DosageAmount,
    string DosageUnit,
    int MilkWithdrawalDays,
    int MeatWithdrawalDays,
    DateOnly StartDate,
    decimal CostBdt,
    string? VeterinarianName,
    string? Notes,
    Guid? InventoryItemId = null,
    decimal? ConsumptionQuantity = null,
    Guid? ConsumptionUnitId = null
) : IRequest<Guid>, ITransactionalCommand;
// ConsumptionQuantity is in ConsumptionUnitId (e.g. 2 "Bottle (10 ml)") and stored in the item's base unit.

public sealed class LogMedicalTreatmentCommandValidator : AbstractValidator<LogMedicalTreatmentCommand>
{
    public LogMedicalTreatmentCommandValidator()
    {
        RuleFor(v => v.AnimalId).NotEmpty();
        RuleFor(v => v.Diagnosis).NotEmpty().MaximumLength(250);
        RuleFor(v => v.MedicationName).NotEmpty().MaximumLength(150);
        RuleFor(v => v.DosageAmount).GreaterThan(0);
        RuleFor(v => v.DosageUnit).NotEmpty().MaximumLength(20);
        RuleFor(v => v.MilkWithdrawalDays).GreaterThanOrEqualTo(0);
        RuleFor(v => v.MeatWithdrawalDays).GreaterThanOrEqualTo(0);
        RuleFor(v => v.CostBdt).GreaterThanOrEqualTo(0);
        RuleFor(v => v.VeterinarianName).MaximumLength(150);
        RuleFor(v => v.Notes).MaximumLength(1000);
    }
}

internal sealed class LogMedicalTreatmentCommandHandler(
    IMedicalTreatmentRepository medicalTreatmentRepository,
    IAnimalRepository animalRepository,
    ITenantService tenantService,
    IUnitOfWork unitOfWork,
    IPublisher publisher,
    Farm360.Domain.Inventory.Interfaces.Repositories.IInventoryItemRepository? inventoryItems = null,
    Farm360.Application.Inventory.Units.IUnitService? units = null) : IRequestHandler<LogMedicalTreatmentCommand, Guid>
{
    public async Task<Guid> Handle(LogMedicalTreatmentCommand request, CancellationToken cancellationToken)
    {
        var animal = await animalRepository.GetByIdAsync(request.AnimalId, cancellationToken)
            ?? throw new KeyNotFoundException($"Animal with ID '{request.AnimalId}' was not found.");

        if (animal.Status == AnimalStatus.Dead || animal.Status == AnimalStatus.Sold || animal.Status == AnimalStatus.Slaughtered)
            throw new DeceasedAnimalHealthRecordException(animal.Tag.TagId);

        // Check for active treatment of same drug (PRD BRU-HV-03)
        var hasActiveTreatment = await medicalTreatmentRepository.HasActiveTreatmentForMedicationAsync(
            request.AnimalId, request.MedicationName, cancellationToken);

        if (hasActiveTreatment)
            throw new OverlappingTreatmentException(animal.Tag.TagId, request.MedicationName);

        // Used from stock in a pack unit: stock, cost and the treatment record keep the base unit.
        var consumption = request.ConsumptionQuantity;
        if (consumption is > 0 && request.ConsumptionUnitId is { } unitId && request.InventoryItemId is { } itemId
            && inventoryItems is not null && units is not null)
        {
            var item = await inventoryItems.GetByIdAsync(itemId, cancellationToken)
                ?? throw new KeyNotFoundException($"Inventory item '{itemId}' was not found.");
            consumption = (await units.ToBaseAsync(item, unitId, consumption.Value, 0m, cancellationToken)).BaseQuantity;
        }

        var dosage = Dosage.Create(request.DosageAmount, request.DosageUnit);
        var withdrawal = WithdrawalPeriod.Create(request.MilkWithdrawalDays, request.MeatWithdrawalDays);

        var treatment = MedicalTreatment.LogTreatment(
            tenantService.TenantId,
            request.AnimalId,
            request.Diagnosis,
            request.MedicationName,
            dosage,
            withdrawal,
            request.StartDate,
            null, // Ongoing
            request.CostBdt,
            request.VeterinarianName,
            request.Notes,
            request.InventoryItemId,
            consumption);

        medicalTreatmentRepository.Add(treatment);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var domainEvents = treatment.DomainEvents.ToList();
        treatment.ClearDomainEvents();
        foreach (var domainEvent in domainEvents)
        {
            await publisher.Publish(domainEvent, cancellationToken);
        }

        return treatment.Id;
    }
}
