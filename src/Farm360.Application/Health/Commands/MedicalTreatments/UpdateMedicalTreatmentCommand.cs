using Farm360.Application.Common.Behaviors;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Health.Enums;
using Farm360.Domain.Health.Interfaces.Repositories;
using Farm360.Domain.Health.ValueObjects;
using FluentValidation;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Application.Health.Commands.MedicalTreatments;

public sealed record UpdateMedicalTreatmentCommand(
    Guid TreatmentId,
    string Diagnosis,
    string MedicationName,
    decimal DosageAmount,
    string DosageUnit,
    int MilkWithdrawalDays,
    int MeatWithdrawalDays,
    DateOnly StartDate,
    DateOnly? EndDate,
    decimal CostBdt,
    string? VeterinarianName,
    string? Notes,
    TreatmentStatus Status,
    Guid? InventoryItemId = null,
    decimal? ConsumptionQuantity = null
) : IRequest, ITransactionalCommand;

public sealed class UpdateMedicalTreatmentCommandValidator : AbstractValidator<UpdateMedicalTreatmentCommand>
{
    public UpdateMedicalTreatmentCommandValidator()
    {
        RuleFor(v => v.TreatmentId).NotEmpty();
        RuleFor(v => v.Diagnosis).NotEmpty().MaximumLength(250);
        RuleFor(v => v.MedicationName).NotEmpty().MaximumLength(150);
        RuleFor(v => v.DosageAmount).GreaterThan(0);
        RuleFor(v => v.DosageUnit).NotEmpty().MaximumLength(20);
        RuleFor(v => v.MilkWithdrawalDays).GreaterThanOrEqualTo(0);
        RuleFor(v => v.MeatWithdrawalDays).GreaterThanOrEqualTo(0);
        RuleFor(v => v.CostBdt).GreaterThanOrEqualTo(0);
        RuleFor(v => v.VeterinarianName).MaximumLength(150);
        RuleFor(v => v.Notes).MaximumLength(1000);
        RuleFor(v => v.Status).IsInEnum();
    }
}

internal sealed class UpdateMedicalTreatmentCommandHandler(
    IMedicalTreatmentRepository medicalTreatmentRepository,
    ITenantService tenantService,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateMedicalTreatmentCommand>
{
    public async Task Handle(UpdateMedicalTreatmentCommand request, CancellationToken cancellationToken)
    {
        var treatment = await medicalTreatmentRepository.GetByIdAsync(request.TreatmentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Medical treatment with ID '{request.TreatmentId}' was not found.");

        if (treatment.TenantId != tenantService.TenantId)
            throw new UnauthorizedAccessException("Treatment does not belong to current tenant.");

        var dosage = Dosage.Create(request.DosageAmount, request.DosageUnit);
        var withdrawal = WithdrawalPeriod.Create(request.MilkWithdrawalDays, request.MeatWithdrawalDays);

        treatment.UpdateDetails(
            request.Diagnosis,
            request.MedicationName,
            dosage,
            withdrawal,
            request.StartDate,
            request.EndDate,
            request.CostBdt,
            request.VeterinarianName,
            request.Notes,
            request.Status,
            request.InventoryItemId,
            request.ConsumptionQuantity);

        medicalTreatmentRepository.Update(treatment);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
