using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Health;
using Farm360.Domain.Health.Enums;
using Farm360.Domain.Health.Interfaces.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Health.Commands.VetVisits;

public sealed record UpdateVetVisitCommand(
    Guid Id,
    string VetName,
    DateOnly VisitDate,
    VetVisitType VisitType,
    string? Purpose,
    string? Findings,
    string? Recommendations,
    decimal? CostBdt,
    DateOnly? NextVisitDate
) : IRequest;

public sealed class UpdateVetVisitCommandValidator : AbstractValidator<UpdateVetVisitCommand>
{
    public UpdateVetVisitCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.VetName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.VisitDate).NotEmpty();
        RuleFor(x => x.VisitType).IsInEnum();
        RuleFor(x => x.Purpose).MaximumLength(250);
        RuleFor(x => x.Findings).MaximumLength(1000);
        RuleFor(x => x.Recommendations).MaximumLength(1000);
        RuleFor(x => x.CostBdt).GreaterThanOrEqualTo(0).When(x => x.CostBdt.HasValue);
    }
}

internal sealed class UpdateVetVisitCommandHandler : IRequestHandler<UpdateVetVisitCommand>
{
    private readonly IVetVisitRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateVetVisitCommandHandler(
        IVetVisitRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdateVetVisitCommand request, CancellationToken cancellationToken)
    {
        var visit = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(VetVisit), request.Id);

        visit.Update(
            request.VetName,
            request.VisitDate,
            request.VisitType,
            request.Purpose,
            request.Findings,
            request.Recommendations,
            request.CostBdt,
            request.NextVisitDate
        );

        _repository.Update(visit);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
