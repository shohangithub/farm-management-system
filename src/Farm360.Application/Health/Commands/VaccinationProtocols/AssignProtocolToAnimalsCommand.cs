using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Health;
using Farm360.Domain.Health.Interfaces.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Health.Commands.VaccinationProtocols;

public sealed record AssignProtocolToAnimalsCommand(
    Guid ProtocolId,
    IReadOnlyList<Guid> AnimalIds,
    DateOnly StartDate
) : IRequest;

public sealed class AssignProtocolToAnimalsCommandValidator : AbstractValidator<AssignProtocolToAnimalsCommand>
{
    public AssignProtocolToAnimalsCommandValidator()
    {
        RuleFor(x => x.ProtocolId).NotEmpty();
        RuleFor(x => x.AnimalIds).NotEmpty();
        RuleFor(x => x.StartDate).NotEmpty();
    }
}

internal sealed class AssignProtocolToAnimalsCommandHandler : IRequestHandler<AssignProtocolToAnimalsCommand>
{
    private readonly IVaccinationRepository _repository;
    private readonly ITenantService _tenantService;
    private readonly IUnitOfWork _unitOfWork;

    public AssignProtocolToAnimalsCommandHandler(
        IVaccinationRepository repository,
        ITenantService tenantService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _tenantService = tenantService;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(AssignProtocolToAnimalsCommand request, CancellationToken cancellationToken)
    {
        var protocol = await _repository.GetProtocolByIdAsync(request.ProtocolId, cancellationToken);

        if (protocol == null || protocol.TenantId != _tenantService.TenantId)
            throw new KeyNotFoundException("Protocol not found.");

        var events = new List<VaccinationEvent>();

        foreach (var animalId in request.AnimalIds)
        {
            foreach (var step in protocol.Steps)
            {
                var scheduledDate = request.StartDate.AddDays(step.TargetAgeDays);

                var @event = VaccinationEvent.Schedule(
                    _tenantService.TenantId,
                    animalId,
                    step.Id,
                    step.VaccineName,
                    string.Empty,
                    scheduledDate,
                    $"Scheduled from protocol '{protocol.Title}' - Step '{step.StepName}'");

                events.Add(@event);
            }
        }

        _repository.AddEvents(events);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
