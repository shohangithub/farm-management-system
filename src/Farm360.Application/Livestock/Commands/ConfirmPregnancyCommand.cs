using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Livestock.Repositories;
using MediatR;

namespace Farm360.Application.Livestock.Commands;

public sealed record ConfirmPregnancyCommand(
    Guid AnimalId,
    Guid BreedingRecordId,
    DateOnly ConfirmDate,
    DateOnly ExpectedCalvingDate) : IRequest;

public sealed class ConfirmPregnancyCommandHandler : IRequestHandler<ConfirmPregnancyCommand>
{
    private readonly IAnimalRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ConfirmPregnancyCommandHandler(
        IAnimalRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(ConfirmPregnancyCommand request, CancellationToken cancellationToken)
    {
        var animal = await _repository.GetByIdAsync(request.AnimalId, cancellationToken)
            ?? throw new ArgumentException($"Animal '{request.AnimalId}' not found.");

        animal.ConfirmPregnancy(request.BreedingRecordId, request.ConfirmDate, request.ExpectedCalvingDate);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
