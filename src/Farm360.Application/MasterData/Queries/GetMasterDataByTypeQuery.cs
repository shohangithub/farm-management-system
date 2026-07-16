using Farm360.Application.Common.Interfaces;
using Farm360.Application.MasterData.DTOs;
using Farm360.Domain.MasterData.Enums;
using Farm360.Domain.MasterData.Repositories;
using MediatR;

namespace Farm360.Application.MasterData.Queries;

public record GetMasterDataByTypeQuery(int Type) : IRequest<IReadOnlyList<MasterDataDto>>;

public class GetMasterDataByTypeQueryHandler : IRequestHandler<GetMasterDataByTypeQuery, IReadOnlyList<MasterDataDto>>
{
    private readonly IMasterDataRepository _repository;
    private readonly ITenantService _tenantService;

    public GetMasterDataByTypeQueryHandler(IMasterDataRepository repository, ITenantService tenantService)
    {
        _repository = repository;
        _tenantService = tenantService;
    }

    public async Task<IReadOnlyList<MasterDataDto>> Handle(GetMasterDataByTypeQuery request, CancellationToken cancellationToken)
    {
        var entries = await _repository.GetAllByTypeAsync(
            _tenantService.TenantId, 
            (MasterDataType)request.Type, 
            cancellationToken);

        return entries.Select(e => e.ToDto()).ToList();
    }
}
