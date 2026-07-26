using Farm360.Application.Common.Interfaces;
using Farm360.Application.Livestock.DTOs;
using Farm360.Domain.Livestock.Repositories;
using MediatR;

namespace Farm360.Application.Livestock.Commands;

public sealed record RecordBcsCommand(
    Guid AnimalId,
    decimal Score,
    DateOnly RecordedDate,
    string? Notes) : IRequest<BcsRecordDto>;

public sealed class RecordBcsCommandHandler : IRequestHandler<RecordBcsCommand, BcsRecordDto>
{
    private readonly IAnimalRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public RecordBcsCommandHandler(
        IAnimalRepository repository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<BcsRecordDto> Handle(RecordBcsCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.TenantId ?? throw new UnauthorizedAccessException("Tenant context is missing.");
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException("User context is missing.");

        var animal = await _repository.GetByIdAsync(request.AnimalId, cancellationToken)
            ?? throw new ArgumentException("Animal not found.");

        if (animal.TenantId != tenantId)
            throw new UnauthorizedAccessException("Animal does not belong to the current tenant.");

        var bcs = animal.RecordBodyConditionScore(
            request.Score,
            request.RecordedDate,
            userId,
            request.Notes);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return bcs.ToDto();
    }
}
