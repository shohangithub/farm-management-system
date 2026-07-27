using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Livestock.DTOs;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Livestock.Commands;

public sealed record RecordBcsCommand(
    Guid AnimalId,
    decimal Score,
    DateOnly RecordedDate,
    string? Notes) : IRequest<BcsRecordDto>;

public sealed class RecordBcsCommandValidator : AbstractValidator<RecordBcsCommand>
{
    public RecordBcsCommandValidator()
    {
        RuleFor(x => x.AnimalId).NotEmpty();
        
        RuleFor(x => x.Score)
            .InclusiveBetween(1.0m, 5.0m).WithMessage("BCS score must be between 1.0 and 5.0.");
            
        RuleFor(x => x.RecordedDate)
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Recorded date cannot be in the future.");
            
        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("Notes cannot exceed 500 characters.")
            .When(x => x.Notes is not null);
    }
}

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
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException("User context is missing.");

        var animal = await _repository.GetByIdAsync(request.AnimalId, cancellationToken)
            ?? throw new NotFoundException(nameof(Animal), request.AnimalId);

        var bcs = animal.RecordBodyConditionScore(
            request.Score,
            request.RecordedDate,
            userId,
            request.Notes);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return bcs.ToDto();
    }
}
