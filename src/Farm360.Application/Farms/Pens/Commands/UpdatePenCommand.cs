using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Farms.Enums;
using Farm360.Domain.Farms.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Farms.Pens.Commands;

public record UpdatePenCommand(
    Guid Id,
    string PenName,
    int Capacity,
    string? AnimalGroup,
    string? Notes,
    int Status) : IRequest;

public class UpdatePenCommandValidator : AbstractValidator<UpdatePenCommand>
{
    public UpdatePenCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
        RuleFor(v => v.PenName).NotEmpty().MaximumLength(200);
        RuleFor(v => v.Capacity).GreaterThanOrEqualTo(0);
        RuleFor(v => v.AnimalGroup).MaximumLength(100);
        RuleFor(v => v.Notes).MaximumLength(500);
        RuleFor(v => v.Status).InclusiveBetween(1, 3);
    }
}

public class UpdatePenCommandHandler : IRequestHandler<UpdatePenCommand>
{
    private readonly IPenRepository _repository;
    private readonly ITenantService _tenantService;
    private readonly IUnitOfWork _unitOfWork;

    public UpdatePenCommandHandler(
        IPenRepository repository,
        ITenantService tenantService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _tenantService = tenantService;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdatePenCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantService.TenantId;

        var pen = await _repository.GetByIdAsync(tenantId, request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Farms.Pen), request.Id);

        pen.UpdateDetails(
            request.PenName,
            request.Capacity,
            request.AnimalGroup,
            request.Notes);

        pen.ChangeStatus((PenStatus)request.Status);

        _repository.Update(pen);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
