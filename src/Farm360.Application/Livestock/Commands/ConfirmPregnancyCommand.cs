using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Livestock.Commands;

public sealed record ConfirmPregnancyCommand(
    Guid AnimalId,
    Guid BreedingRecordId,
    DateOnly ConfirmDate,
    DateOnly ExpectedCalvingDate) : IRequest;

public sealed class ConfirmPregnancyCommandValidator : AbstractValidator<ConfirmPregnancyCommand>
{
    public ConfirmPregnancyCommandValidator()
    {
        RuleFor(x => x.AnimalId).NotEmpty();
        RuleFor(x => x.BreedingRecordId).NotEmpty();
        
        RuleFor(x => x.ConfirmDate)
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Pregnancy confirmation date cannot be in the future.");
            
        RuleFor(x => x.ExpectedCalvingDate)
            .GreaterThan(x => x.ConfirmDate)
            .WithMessage("Expected calving date must be after confirmation date.");
    }
}

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
            ?? throw new NotFoundException(nameof(Animal), request.AnimalId);

        animal.ConfirmPregnancy(request.BreedingRecordId, request.ConfirmDate, request.ExpectedCalvingDate);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
