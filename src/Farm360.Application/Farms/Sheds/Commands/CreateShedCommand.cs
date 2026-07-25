using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Farms;
using Farm360.Domain.Farms.Repositories;
using FluentValidation;
using MediatR;
using Farm360.Application.Common.Behaviors;

namespace Farm360.Application.Farms.Sheds.Commands;

public sealed record CreateShedCommand(
    Guid FarmId,
    string ShedNumber,
    string ShedName,
    int? Capacity,
    string? AnimalType,
    string? FloorType,
    string? RoofType,
    bool HasVentilation,
    bool HasWaterLine,
    bool HasFeedLine) : IRequest<Guid>, ITransactionalCommand;

public sealed class CreateShedCommandValidator : AbstractValidator<CreateShedCommand>
{
    public CreateShedCommandValidator()
    {
        RuleFor(x => x.FarmId).NotEmpty();
        RuleFor(x => x.ShedNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ShedName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Capacity).GreaterThanOrEqualTo(0).When(x => x.Capacity.HasValue);
        RuleFor(x => x.AnimalType).MaximumLength(100);
        RuleFor(x => x.FloorType).MaximumLength(100);
        RuleFor(x => x.RoofType).MaximumLength(100);
    }
}

public sealed class CreateShedCommandHandler : IRequestHandler<CreateShedCommand, Guid>
{
    private readonly IShedRepository _repository;
    private readonly IFarmRepository _farmRepository;
    private readonly ITenantService _tenantService;
    private readonly IUnitOfWork _unitOfWork;

    public CreateShedCommandHandler(
        IShedRepository repository,
        IFarmRepository farmRepository,
        ITenantService tenantService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _farmRepository = farmRepository;
        _tenantService = tenantService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreateShedCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantService.TenantId;

        // Check if farm exists
        var farmExists = await _farmRepository.GetByIdAsync(tenantId, request.FarmId, cancellationToken)
            ?? throw new Farm360.Application.Common.Exceptions.ValidationException(new[] { new FluentValidation.Results.ValidationFailure("FarmId", "Farm not found.") });

        // Check if shed number is unique
        var exists = await _repository.ExistsByNumberAsync(tenantId, request.FarmId, request.ShedNumber, cancellationToken);
        if (exists)
            throw new Farm360.Application.Common.Exceptions.ValidationException(new[] { new FluentValidation.Results.ValidationFailure("ShedNumber", "A shed with this number already exists in the farm.") });

        var shed = Shed.Create(
            tenantId,
            request.FarmId,
            request.ShedNumber,
            request.ShedName,
            request.Capacity,
            request.AnimalType,
            request.FloorType,
            request.RoofType,
            request.HasVentilation,
            request.HasWaterLine,
            request.HasFeedLine);

        _repository.Add(shed);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return shed.Id;
    }
}
