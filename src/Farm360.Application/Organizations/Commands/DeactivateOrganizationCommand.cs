using Farm360.Application.Common.Behaviors;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Organizations;
using Farm360.Domain.Organizations.Repositories;
using MediatR;

namespace Farm360.Application.Organizations.Commands;

public record DeactivateOrganizationCommand(Guid Id) : IRequest, ITransactionalCommand;

internal sealed class DeactivateOrganizationCommandHandler : IRequestHandler<DeactivateOrganizationCommand>
{
    private readonly IOrganizationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DeactivateOrganizationCommandHandler(
        IOrganizationRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeactivateOrganizationCommand request, CancellationToken cancellationToken)
    {
        var organization = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Organization), request.Id);

        organization.Deactivate();

        _repository.Update(organization);

        // SaveChangesAsync persists within the pipeline-managed transaction (TransactionBehavior).
        // Do NOT call BeginTransactionAsync here — the MediatR TransactionBehavior already wraps this command.
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
