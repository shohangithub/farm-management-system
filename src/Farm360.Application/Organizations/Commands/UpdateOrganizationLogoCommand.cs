using Farm360.Application.Common.Behaviors;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Organizations.Repositories;
using MediatR;

namespace Farm360.Application.Organizations.Commands;

public record UpdateOrganizationLogoCommand(Guid OrganizationId, string LogoUrl) : IRequest, ITransactionalCommand;

internal sealed class UpdateOrganizationLogoCommandHandler : IRequestHandler<UpdateOrganizationLogoCommand>
{
    private readonly IOrganizationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateOrganizationLogoCommandHandler(IOrganizationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdateOrganizationLogoCommand request, CancellationToken cancellationToken)
    {
        var organization = await _repository.GetByIdAsync(request.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Organization {request.OrganizationId} not found.");

        organization.Update(
            organization.Name,
            request.LogoUrl,
            organization.ContactEmail,
            organization.ContactPhone,
            organization.BusinessRegistrationNumber,
            organization.TradeLicenseNumber,
            organization.TaxIdentificationNumber,
            organization.CurrencyCode,
            organization.TimeZoneId,
            organization.LanguageCode,
            organization.Address,
            organization.BusinessType
        );
        
        _repository.Update(organization);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
