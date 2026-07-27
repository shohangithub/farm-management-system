using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Livestock.Commands;

public sealed record CreateBatchCommand(
    Guid FarmId,
    string Name,
    string? Notes) : IRequest<Guid>;

public sealed class CreateBatchCommandValidator : AbstractValidator<CreateBatchCommand>
{
    public CreateBatchCommandValidator()
    {
        RuleFor(x => x.FarmId).NotEmpty();
        
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name cannot exceed 100 characters.");
            
        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("Notes cannot exceed 500 characters.")
            .When(x => x.Notes is not null);
    }
}

public sealed class CreateBatchCommandHandler : IRequestHandler<CreateBatchCommand, Guid>
{
    private readonly IAnimalBatchRepository _batchRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public CreateBatchCommandHandler(
        IAnimalBatchRepository batchRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser)
    {
        _batchRepository = batchRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(CreateBatchCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.TenantId ?? throw new UnauthorizedAccessException("Tenant context is missing.");

        var batch = AnimalBatch.Create(tenantId, request.FarmId, request.Name, request.Notes);
        
        await _batchRepository.AddAsync(batch, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return batch.Id;
    }
}
