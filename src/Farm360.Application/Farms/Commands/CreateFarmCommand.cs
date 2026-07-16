using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Farms;
using Farm360.Domain.Farms.Enums;
using Farm360.Domain.Farms.Repositories;
using Farm360.Domain.Organizations.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Farms.Commands;

public sealed record CreateFarmCommand(
    Guid BranchId,
    string FarmCode,
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
    string? Description) : IRequest<Guid>;

public sealed class CreateFarmCommandValidator : AbstractValidator<CreateFarmCommand>
{
    public CreateFarmCommandValidator()
    {
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.FarmCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.FarmName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Capacity).GreaterThanOrEqualTo(0).When(x => x.Capacity.HasValue);
        RuleFor(x => x.OwnerId).MaximumLength(36);
        RuleFor(x => x.ManagerId).MaximumLength(36);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

public sealed class CreateFarmCommandHandler : IRequestHandler<CreateFarmCommand, Guid>
{
    private readonly IFarmRepository _repository;
    private readonly IBranchRepository _branchRepository;
    private readonly ITenantService _tenantService;
    private readonly IUnitOfWork _unitOfWork;

    public CreateFarmCommandHandler(
        IFarmRepository repository,
        IBranchRepository branchRepository,
        ITenantService tenantService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _branchRepository = branchRepository;
        _tenantService = tenantService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreateFarmCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantService.TenantId;

        // Check if branch exists
        var branchExists = await _branchRepository.GetByIdAsync(tenantId, request.BranchId, cancellationToken)
            ?? throw new Farm360.Application.Common.Exceptions.ValidationException(new[] { new FluentValidation.Results.ValidationFailure("BranchId", "Branch not found.") });

        // Check if farm code is unique
        var exists = await _repository.ExistsByCodeAsync(tenantId, request.FarmCode, cancellationToken);
        if (exists)
            throw new Farm360.Application.Common.Exceptions.ValidationException(new[] { new FluentValidation.Results.ValidationFailure("FarmCode", "A farm with this code already exists for the current tenant.") });

        var farm = Farm.Create(
            tenantId,
            request.BranchId,
            request.FarmCode,
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

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _repository.Add(farm);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(transaction, cancellationToken);
            return farm.Id;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(transaction, cancellationToken);
            throw;
        }
    }
}
