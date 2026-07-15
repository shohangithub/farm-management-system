using Farm360.Application.Common.Exceptions;
using Farm360.Application.Organizations.DTOs;
using Farm360.Domain.Organizations;
using Farm360.Domain.Organizations.Repositories;
using MediatR;

namespace Farm360.Application.Organizations.Queries;

public record GetOrganizationByIdQuery(Guid Id) : IRequest<OrganizationDto>;

internal sealed class GetOrganizationByIdQueryHandler : IRequestHandler<GetOrganizationByIdQuery, OrganizationDto>
{
    private readonly IOrganizationRepository _repository;

    public GetOrganizationByIdQueryHandler(IOrganizationRepository repository)
    {
        _repository = repository;
    }

    public async Task<OrganizationDto> Handle(GetOrganizationByIdQuery request, CancellationToken cancellationToken)
    {
        var organization = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Organization), request.Id);

        return organization.ToDto();
    }
}
