using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Farms.Pens.DTOs;
using Farm360.Domain.Farms.Repositories;
using MediatR;

namespace Farm360.Application.Farms.Pens.Queries;

public record GetPenByIdQuery(Guid Id) : IRequest<PenDto>;

public class GetPenByIdQueryHandler : IRequestHandler<GetPenByIdQuery, PenDto>
{
    private readonly IPenRepository _repository;
    private readonly ITenantService _tenantService;

    public GetPenByIdQueryHandler(IPenRepository repository, ITenantService tenantService)
    {
        _repository = repository;
        _tenantService = tenantService;
    }

    public async Task<PenDto> Handle(GetPenByIdQuery request, CancellationToken cancellationToken)
    {
        var pen = await _repository.GetByIdAsync(_tenantService.TenantId, request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Farms.Pen), request.Id);

        return pen.ToDto();
    }
}
