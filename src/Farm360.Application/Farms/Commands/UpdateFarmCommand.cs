using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Farms.Enums;
using Farm360.Domain.Farms.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Farms.Commands;

public sealed record UpdateFarmCommand(
    Guid Id,
    string FarmName,
    FarmType Type,
    double? FarmSize,
    double? LandArea,
    double? Latitude,
    double? Longitude,
    string? MapPolygon,
    int? Capacity,
    string? OwnerId,
    string? ManagerId,
    FarmStatus Status,
    string? Description) : IRequest;

public sealed class UpdateFarmCommandValidator : AbstractValidator<UpdateFarmCommand>
{
    public UpdateFarmCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.FarmName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Capacity).GreaterThanOrEqualTo(0).When(x => x.Capacity.HasValue);
        RuleFor(x => x.OwnerId).MaximumLength(36);
        RuleFor(x => x.ManagerId).MaximumLength(36);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

public sealed class UpdateFarmCommandHandler : IRequestHandler<UpdateFarmCommand>
{
    private readonly IFarmRepository _repository;
    private readonly ITenantService _tenantService;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateFarmCommandHandler(
        IFarmRepository repository,
        ITenantService tenantService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _tenantService = tenantService;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdateFarmCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantService.TenantId;

        var farm = await _repository.GetByIdAsync(tenantId, request.Id, cancellationToken)
            ?? throw new Farm360.Application.Common.Exceptions.NotFoundException(nameof(Domain.Farms.Farm), request.Id);

        farm.UpdateDetails(
            request.FarmName,
            request.Type,
            request.FarmSize,
            request.LandArea,
            request.Latitude,
            request.Longitude,
            request.MapPolygon,
            request.Capacity,
            request.OwnerId,
            request.ManagerId,
            request.Description);

        farm.ChangeStatus(request.Status);

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _repository.Update(farm);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(transaction, cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(transaction, cancellationToken);
            throw;
        }
    }
}
