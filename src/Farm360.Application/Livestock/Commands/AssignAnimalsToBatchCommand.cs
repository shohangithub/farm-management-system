using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Livestock.Repositories;
using MediatR;

namespace Farm360.Application.Livestock.Commands;

public sealed record AssignAnimalsToBatchCommand(
    Guid BatchId,
    IReadOnlyList<Guid> AnimalIds) : IRequest;

public sealed class AssignAnimalsToBatchCommandHandler : IRequestHandler<AssignAnimalsToBatchCommand>
{
    private readonly IAnimalBatchRepository _batchRepository;
    private readonly IAnimalRepository _animalRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public AssignAnimalsToBatchCommandHandler(
        IAnimalBatchRepository batchRepository,
        IAnimalRepository animalRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser)
    {
        _batchRepository = batchRepository;
        _animalRepository = animalRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task Handle(AssignAnimalsToBatchCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.TenantId ?? throw new UnauthorizedAccessException("Tenant context is missing.");

        var batch = await _batchRepository.GetByIdAsync(request.BatchId, cancellationToken)
            ?? throw new ArgumentException("Batch not found.");

        if (batch.TenantId != tenantId)
            throw new UnauthorizedAccessException("Batch does not belong to the current tenant.");

        foreach (var animalId in request.AnimalIds)
        {
            var animal = await _animalRepository.GetByIdAsync(animalId, cancellationToken);
            if (animal != null && animal.TenantId == tenantId)
            {
                animal.AssignToBatch(batch.Id);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
