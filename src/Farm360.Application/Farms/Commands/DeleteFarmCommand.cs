using Farm360.Application.Common.Behaviors;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Farms.Repositories;
using MediatR;

namespace Farm360.Application.Farms.Commands;

public sealed record DeleteFarmCommand(Guid Id) : IRequest, ITransactionalCommand;

public sealed class DeleteFarmCommandHandler : IRequestHandler<DeleteFarmCommand>
{
    private readonly IFarmRepository _repository;
    private readonly ITenantService _tenantService;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteFarmCommandHandler(
        IFarmRepository repository,
        ITenantService tenantService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _tenantService = tenantService;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeleteFarmCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantService.TenantId;

        var farm = await _repository.GetByIdAsync(tenantId, request.Id, cancellationToken)
            ?? throw new Farm360.Application.Common.Exceptions.NotFoundException(nameof(Domain.Farms.Farm), request.Id);

        _repository.Delete(farm);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
