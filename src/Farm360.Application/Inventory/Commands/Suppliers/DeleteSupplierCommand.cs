using Farm360.Application.Common.Interfaces;
using Farm360.Application.Common.Exceptions;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Inventory.Commands.Suppliers;

public sealed record DeleteSupplierCommand(Guid Id) : IRequest;

internal sealed class DeleteSupplierCommandHandler : IRequestHandler<DeleteSupplierCommand>
{
    private readonly ISupplierRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteSupplierCommandHandler(ISupplierRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeleteSupplierCommand request, CancellationToken cancellationToken)
    {
        var supplier = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("Supplier", request.Id);

        _repository.Delete(supplier);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
