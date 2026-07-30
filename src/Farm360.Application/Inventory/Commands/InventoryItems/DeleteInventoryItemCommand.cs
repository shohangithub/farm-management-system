using Farm360.Application.Common.Interfaces;
using Farm360.Application.Common.Exceptions;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Inventory.Commands.InventoryItems;

public sealed record DeleteInventoryItemCommand(Guid Id) : IRequest;

internal sealed class DeleteInventoryItemCommandHandler : IRequestHandler<DeleteInventoryItemCommand>
{
    private readonly IInventoryItemRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteInventoryItemCommandHandler(IInventoryItemRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeleteInventoryItemCommand request, CancellationToken cancellationToken)
    {
        var item = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("InventoryItem", request.Id);

        _repository.Delete(item);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
