using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Health.Enums;
using Farm360.Domain.Health.Interfaces.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Health.Commands.MedicalTreatments;

public sealed record UpdateTreatmentStatusCommand(
    Guid TreatmentId,
    TreatmentStatus Status,
    string? Notes
) : IRequest;

public sealed class UpdateTreatmentStatusCommandValidator : AbstractValidator<UpdateTreatmentStatusCommand>
{
    public UpdateTreatmentStatusCommandValidator()
    {
        RuleFor(x => x.TreatmentId).NotEmpty();
        RuleFor(x => x.Status).IsInEnum();
    }
}

internal sealed class UpdateTreatmentStatusCommandHandler : IRequestHandler<UpdateTreatmentStatusCommand>
{
    private readonly IFarmClock _clock;

    private readonly IMedicalTreatmentRepository _repository;
    private readonly ITenantService _tenantService;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateTreatmentStatusCommandHandler(
        IMedicalTreatmentRepository repository,
        ITenantService tenantService,
        IUnitOfWork unitOfWork,
        IFarmClock? clock = null)
    {
        _clock = clock ?? Farm360.Application.Common.Time.FarmClock.System;
        _repository = repository;
        _tenantService = tenantService;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdateTreatmentStatusCommand request, CancellationToken cancellationToken)
    {
        var treatment = await _repository.GetByIdAsync(request.TreatmentId, cancellationToken);

        if (treatment == null || treatment.TenantId != _tenantService.TenantId)
            throw new ArgumentException("Treatment not found.");

        // Only a running treatment can be completed or failed. A treatment closed when the animal
        // was sold or died keeps its end date (the exit date) and note.
        if (treatment.Status != TreatmentStatus.Ongoing)
            throw new Farm360.Domain.Exceptions.DomainException($"This treatment is already {treatment.Status.ToString().ToLowerInvariant()}.");

        if (request.Status == TreatmentStatus.Completed)
        {
            treatment.CompleteTreatment(_clock.Today, request.Notes);
        }
        else if (request.Status == TreatmentStatus.Failed)
        {
            treatment.MarkFailed(request.Notes ?? "Marked as failed without notes");
        }
        else
        {
            throw new ArgumentException("Invalid status transition.");
        }

        _repository.Update(treatment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
