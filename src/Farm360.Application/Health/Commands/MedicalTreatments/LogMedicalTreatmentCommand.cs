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
    string? Notes
) : IRequest<Guid>;

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
    IUnitOfWork unitOfWork) : IRequestHandler<LogMedicalTreatmentCommand, Guid>
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
            request.Notes);

        medicalTreatmentRepository.Add(treatment);
        
        await using var tx = await unitOfWork.BeginTransactionAsync(cancellationToken);
        await unitOfWork.CommitTransactionAsync(tx, cancellationToken);

        return treatment.Id;
    }
}
