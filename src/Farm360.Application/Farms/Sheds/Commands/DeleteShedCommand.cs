using Farm360.Application.Common.Behaviors;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Farms.Repositories;
using MediatR;

namespace Farm360.Application.Farms.Sheds.Commands;

public sealed record DeleteShedCommand(Guid Id) : IRequest, ITransactionalCommand;

public sealed class DeleteShedCommandHandler : IRequestHandler<DeleteShedCommand>
{
    private readonly IShedRepository _repository;
    private readonly ITenantService _tenantService;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteShedCommandHandler(
        IShedRepository repository,
        ITenantService tenantService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _tenantService = tenantService;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeleteShedCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantService.TenantId;

        var shed = await _repository.GetByIdAsync(tenantId, request.Id, cancellationToken)
            ?? throw new Farm360.Application.Common.Exceptions.NotFoundException(nameof(Domain.Farms.Shed), request.Id);

        _repository.Delete(shed);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
