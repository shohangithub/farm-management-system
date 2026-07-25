using Farm360.Application.Common.Behaviors;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Farms.Repositories;
using MediatR;

namespace Farm360.Application.Farms.Pens.Commands;

public record DeletePenCommand(Guid Id) : IRequest, ITransactionalCommand;

public class DeletePenCommandHandler : IRequestHandler<DeletePenCommand>
{
    private readonly IPenRepository _repository;
    private readonly ITenantService _tenantService;
    private readonly IUnitOfWork _unitOfWork;

    public DeletePenCommandHandler(
        IPenRepository repository,
        ITenantService tenantService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _tenantService = tenantService;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeletePenCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantService.TenantId;

        var pen = await _repository.GetByIdAsync(tenantId, request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Farms.Pen), request.Id);

        _repository.Delete(pen);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
