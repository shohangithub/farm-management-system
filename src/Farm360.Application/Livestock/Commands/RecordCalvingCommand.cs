using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Livestock.Repositories;
using MediatR;

namespace Farm360.Application.Livestock.Commands;

public sealed record RecordCalvingCommand(
    Guid AnimalId,
    Guid BreedingRecordId,
    DateOnly CalvingDate,
    string Outcome,
    int CalvesCount) : IRequest;

public sealed class RecordCalvingCommandHandler : IRequestHandler<RecordCalvingCommand>
{
    private readonly IAnimalRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public RecordCalvingCommandHandler(
        IAnimalRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(RecordCalvingCommand request, CancellationToken cancellationToken)
    {
        var animal = await _repository.GetByIdAsync(request.AnimalId, cancellationToken)
            ?? throw new ArgumentException($"Animal '{request.AnimalId}' not found.");

        animal.RecordCalving(request.BreedingRecordId, request.CalvingDate, request.Outcome, request.CalvesCount);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
