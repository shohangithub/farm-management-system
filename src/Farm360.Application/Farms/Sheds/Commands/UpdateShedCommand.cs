using Farm360.Application.Common.Behaviors;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Farms.Enums;
using Farm360.Domain.Farms.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Farms.Sheds.Commands;

public sealed record UpdateShedCommand(
    Guid Id,
    string ShedName,
    int? Capacity,
    string? AnimalType,
    string? FloorType,
    string? RoofType,
    bool HasVentilation,
    bool HasWaterLine,
    bool HasFeedLine,
    ShedStatus Status) : IRequest, ITransactionalCommand;

public sealed class UpdateShedCommandValidator : AbstractValidator<UpdateShedCommand>
{
    public UpdateShedCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ShedName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Capacity).GreaterThanOrEqualTo(0).When(x => x.Capacity.HasValue);
        RuleFor(x => x.AnimalType).MaximumLength(100);
        RuleFor(x => x.FloorType).MaximumLength(100);
        RuleFor(x => x.RoofType).MaximumLength(100);
        RuleFor(x => x.Status).IsInEnum();
    }
}

public sealed class UpdateShedCommandHandler : IRequestHandler<UpdateShedCommand>
{
    private readonly IShedRepository _repository;
    private readonly ITenantService _tenantService;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateShedCommandHandler(
        IShedRepository repository,
        ITenantService tenantService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _tenantService = tenantService;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdateShedCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantService.TenantId;

        var shed = await _repository.GetByIdAsync(tenantId, request.Id, cancellationToken)
            ?? throw new Farm360.Application.Common.Exceptions.NotFoundException(nameof(Domain.Farms.Shed), request.Id);

        shed.UpdateDetails(
            request.ShedName,
            request.Capacity,
            request.AnimalType,
            request.FloorType,
            request.RoofType,
            request.HasVentilation,
            request.HasWaterLine,
            request.HasFeedLine);

        shed.ChangeStatus(request.Status);

        _repository.Update(shed);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
