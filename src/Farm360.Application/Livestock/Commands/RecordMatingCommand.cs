using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Livestock.Repositories;
using MediatR;

namespace Farm360.Application.Livestock.Commands;

public sealed record RecordMatingCommand(
    Guid AnimalId,
    DateOnly MatingDate,
    Guid? SireAnimalId,
    string? SireExternalId,
    bool IsArtificialInsemination) : IRequest;

public sealed class RecordMatingCommandHandler : IRequestHandler<RecordMatingCommand>
{
    private readonly IAnimalRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public RecordMatingCommandHandler(
        IAnimalRepository repository,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(RecordMatingCommand request, CancellationToken cancellationToken)
    {
        var animal = await _repository.GetByIdAsync(request.AnimalId, cancellationToken)
            ?? throw new ArgumentException($"Animal '{request.AnimalId}' not found.");

        animal.AddBreedingRecord(
            request.MatingDate,
            request.SireAnimalId,
            request.SireExternalId,
            request.IsArtificialInsemination,
            _currentUser.UserId ?? Guid.Empty);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
