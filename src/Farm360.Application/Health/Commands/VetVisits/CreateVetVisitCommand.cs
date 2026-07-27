using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Health;
using Farm360.Domain.Health.Enums;
using Farm360.Domain.Health.Interfaces.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Health.Commands.VetVisits;

public sealed record CreateVetVisitCommand(
    Guid FarmId,
    string VetName,
    DateOnly VisitDate,
    VetVisitType VisitType,
    string? Purpose,
    string? Findings,
    string? Recommendations,
    decimal? CostBdt,
    DateOnly? NextVisitDate
) : IRequest<Guid>;

public sealed class CreateVetVisitCommandValidator : AbstractValidator<CreateVetVisitCommand>
{
    public CreateVetVisitCommandValidator()
    {
        RuleFor(x => x.FarmId).NotEmpty();
        RuleFor(x => x.VetName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.VisitDate).NotEmpty();
        RuleFor(x => x.VisitType).IsInEnum();
    }
}

internal sealed class CreateVetVisitCommandHandler : IRequestHandler<CreateVetVisitCommand, Guid>
{
    private readonly IVetVisitRepository _repository;
    private readonly ITenantService _tenantService;
    private readonly IUnitOfWork _unitOfWork;

    public CreateVetVisitCommandHandler(
        IVetVisitRepository repository,
        ITenantService tenantService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _tenantService = tenantService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreateVetVisitCommand request, CancellationToken cancellationToken)
    {
        var visit = VetVisit.Create(
            _tenantService.TenantId,
            request.FarmId,
            request.VetName,
            request.VisitDate,
            request.VisitType,
            request.Purpose,
            request.Findings,
            request.Recommendations,
            request.CostBdt,
            request.NextVisitDate);

        _repository.Add(visit);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return visit.Id;
    }
}
