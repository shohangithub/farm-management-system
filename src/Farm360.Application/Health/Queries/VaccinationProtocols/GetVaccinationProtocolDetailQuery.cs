using Farm360.Application.Common.Interfaces;
using Farm360.Application.Health.DTOs;
using Farm360.Application.Health.Mappings;
using Farm360.Domain.Health.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Health.Queries.VaccinationProtocols;

public sealed record GetVaccinationProtocolDetailQuery(Guid Id) : IRequest<VaccinationProtocolDto?>;

internal sealed class GetVaccinationProtocolDetailQueryHandler : IRequestHandler<GetVaccinationProtocolDetailQuery, VaccinationProtocolDto?>
{
    private readonly IVaccinationRepository _repository;

    public GetVaccinationProtocolDetailQueryHandler(IVaccinationRepository repository)
    {
        _repository = repository;
    }

    public async Task<VaccinationProtocolDto?> Handle(GetVaccinationProtocolDetailQuery request, CancellationToken cancellationToken)
    {
        var protocol = await _repository.GetProtocolByIdAsync(request.Id, cancellationToken);
        return protocol?.ToDto();
    }
}
