using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Livestock.Commands;

public sealed record RecordCalvingCommand(
    Guid AnimalId,
    Guid BreedingRecordId,
    DateOnly CalvingDate,
    string Outcome,
    int CalvesCount) : IRequest;

public sealed class RecordCalvingCommandValidator : AbstractValidator<RecordCalvingCommand>
{
    public RecordCalvingCommandValidator()
    {
        RuleFor(x => x.AnimalId).NotEmpty();
        RuleFor(x => x.BreedingRecordId).NotEmpty();
        
        RuleFor(x => x.CalvingDate)
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Calving date cannot be in the future.");
            
        RuleFor(x => x.Outcome)
            .NotEmpty().WithMessage("Outcome is required.")
            .MaximumLength(50).WithMessage("Outcome cannot exceed 50 characters.");
            
        RuleFor(x => x.CalvesCount)
            .GreaterThanOrEqualTo(0).WithMessage("Calves count cannot be negative.");
    }
}

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
            ?? throw new NotFoundException(nameof(Animal), request.AnimalId);

        animal.RecordCalving(request.BreedingRecordId, request.CalvingDate, request.Outcome, request.CalvesCount);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
