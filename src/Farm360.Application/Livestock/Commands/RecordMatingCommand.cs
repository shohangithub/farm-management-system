using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Livestock.Commands;

public sealed record RecordMatingCommand(
    Guid AnimalId,
    DateOnly MatingDate,
    Guid? SireAnimalId,
    string? SireExternalId,
    bool IsArtificialInsemination) : IRequest;

public sealed class RecordMatingCommandValidator : AbstractValidator<RecordMatingCommand>
{
    public RecordMatingCommandValidator()
    {
        RuleFor(x => x.AnimalId).NotEmpty();
        
        RuleFor(x => x.MatingDate)
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Mating date cannot be in the future.");
            
        RuleFor(x => x.SireExternalId)
            .MaximumLength(50).WithMessage("Sire External ID cannot exceed 50 characters.")
            .When(x => x.SireExternalId is not null);
    }
}

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
            ?? throw new NotFoundException(nameof(Animal), request.AnimalId);

        animal.AddBreedingRecord(
            request.MatingDate,
            request.SireAnimalId,
            request.SireExternalId,
            request.IsArtificialInsemination,
            _currentUser.UserId ?? Guid.Empty);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
