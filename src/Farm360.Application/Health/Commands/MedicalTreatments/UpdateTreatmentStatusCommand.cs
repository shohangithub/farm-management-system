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
    private readonly IMedicalTreatmentRepository _repository;
    private readonly ITenantService _tenantService;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateTreatmentStatusCommandHandler(
        IMedicalTreatmentRepository repository,
        ITenantService tenantService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _tenantService = tenantService;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdateTreatmentStatusCommand request, CancellationToken cancellationToken)
    {
        var treatment = await _repository.GetByIdAsync(request.TreatmentId, cancellationToken);

        if (treatment == null || treatment.TenantId != _tenantService.TenantId)
            throw new ArgumentException("Treatment not found.");

        if (request.Status == TreatmentStatus.Completed)
        {
            treatment.CompleteTreatment(DateOnly.FromDateTime(DateTime.UtcNow), request.Notes);
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
