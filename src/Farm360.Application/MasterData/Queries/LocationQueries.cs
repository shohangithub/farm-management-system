using Farm360.Application.Common.Interfaces;
using Farm360.Application.MasterData.DTOs;
using Farm360.Domain.MasterData.Repositories;
using MediatR;

namespace Farm360.Application.MasterData.Queries;

public record GetCountriesQuery : IRequest<IReadOnlyList<CountryDto>>;
public record GetDivisionsQuery(Guid CountryId) : IRequest<IReadOnlyList<DivisionDto>>;
public record GetDistrictsQuery(Guid DivisionId) : IRequest<IReadOnlyList<DistrictDto>>;
public record GetUpazilasQuery(Guid DistrictId) : IRequest<IReadOnlyList<UpazilaDto>>;
public record GetUnionsQuery(Guid UpazilaId) : IRequest<IReadOnlyList<UnionDto>>;
public record GetVillagesQuery(Guid UnionId) : IRequest<IReadOnlyList<VillageDto>>;

public class LocationQueryHandlers :
    IRequestHandler<GetCountriesQuery, IReadOnlyList<CountryDto>>,
    IRequestHandler<GetDivisionsQuery, IReadOnlyList<DivisionDto>>,
    IRequestHandler<GetDistrictsQuery, IReadOnlyList<DistrictDto>>,
    IRequestHandler<GetUpazilasQuery, IReadOnlyList<UpazilaDto>>,
    IRequestHandler<GetUnionsQuery, IReadOnlyList<UnionDto>>,
    IRequestHandler<GetVillagesQuery, IReadOnlyList<VillageDto>>
{
    private readonly ILocationRepository _repository;
    private readonly ITenantService _tenantService;

    public LocationQueryHandlers(ILocationRepository repository, ITenantService tenantService)
    {
        _repository = repository;
        _tenantService = tenantService;
    }

    public async Task<IReadOnlyList<CountryDto>> Handle(GetCountriesQuery request, CancellationToken cancellationToken)
    {
        var entities = await _repository.GetCountriesAsync(_tenantService.TenantId, cancellationToken);
        return entities.Select(x => x.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<DivisionDto>> Handle(GetDivisionsQuery request, CancellationToken cancellationToken)
    {
        var entities = await _repository.GetDivisionsAsync(_tenantService.TenantId, request.CountryId, cancellationToken);
        return entities.Select(x => x.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<DistrictDto>> Handle(GetDistrictsQuery request, CancellationToken cancellationToken)
    {
        var entities = await _repository.GetDistrictsAsync(_tenantService.TenantId, request.DivisionId, cancellationToken);
        return entities.Select(x => x.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<UpazilaDto>> Handle(GetUpazilasQuery request, CancellationToken cancellationToken)
    {
        var entities = await _repository.GetUpazilasAsync(_tenantService.TenantId, request.DistrictId, cancellationToken);
        return entities.Select(x => x.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<UnionDto>> Handle(GetUnionsQuery request, CancellationToken cancellationToken)
    {
        var entities = await _repository.GetUnionsAsync(_tenantService.TenantId, request.UpazilaId, cancellationToken);
        return entities.Select(x => x.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<VillageDto>> Handle(GetVillagesQuery request, CancellationToken cancellationToken)
    {
        var entities = await _repository.GetVillagesAsync(_tenantService.TenantId, request.UnionId, cancellationToken);
        return entities.Select(x => x.ToDto()).ToList();
    }
}
