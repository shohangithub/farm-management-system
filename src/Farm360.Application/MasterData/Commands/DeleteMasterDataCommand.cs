using Farm360.Application.Common.Behaviors;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.MasterData.Repositories;
using MediatR;

namespace Farm360.Application.MasterData.Commands;

public record DeleteMasterDataCommand(Guid Id) : IRequest, ITransactionalCommand;

public class DeleteMasterDataCommandHandler : IRequestHandler<DeleteMasterDataCommand>
{
    private readonly IMasterDataRepository _repository;
    private readonly ITenantService _tenantService;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteMasterDataCommandHandler(
        IMasterDataRepository repository,
        ITenantService tenantService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _tenantService = tenantService;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeleteMasterDataCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantService.TenantId;

        var entry = await _repository.GetByIdAsync(tenantId, request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.MasterData.MasterDataEntry), request.Id);

        _repository.Delete(entry);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
