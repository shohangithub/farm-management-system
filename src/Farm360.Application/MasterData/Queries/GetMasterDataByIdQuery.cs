using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.MasterData.DTOs;
using Farm360.Domain.MasterData.Repositories;
using MediatR;

namespace Farm360.Application.MasterData.Queries;

public record GetMasterDataByIdQuery(Guid Id) : IRequest<MasterDataDto>;

public class GetMasterDataByIdQueryHandler : IRequestHandler<GetMasterDataByIdQuery, MasterDataDto>
{
    private readonly IMasterDataRepository _repository;
    private readonly ITenantService _tenantService;

    public GetMasterDataByIdQueryHandler(IMasterDataRepository repository, ITenantService tenantService)
    {
        _repository = repository;
        _tenantService = tenantService;
    }

    public async Task<MasterDataDto> Handle(GetMasterDataByIdQuery request, CancellationToken cancellationToken)
    {
        var entry = await _repository.GetByIdAsync(_tenantService.TenantId, request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.MasterData.MasterDataEntry), request.Id);

        return entry.ToDto();
    }
}
