using Farm360.Application.Common.Behaviors;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Organizations.Repositories;
using MediatR;

namespace Farm360.Application.Organizations.Commands;

public record ActivateOrganizationCommand(Guid Id) : IRequest, ITransactionalCommand;

internal sealed class ActivateOrganizationCommandHandler : IRequestHandler<ActivateOrganizationCommand>
{
    private readonly IOrganizationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ActivateOrganizationCommandHandler(IOrganizationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(ActivateOrganizationCommand request, CancellationToken cancellationToken)
    {
        var organization = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Organization {request.Id} not found.");

        organization.Activate();
        
        _repository.Update(organization);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
