using Farm360.Domain.MasterData.Locations;

namespace Farm360.Application.MasterData.DTOs;

public record CountryDto(Guid Id, string Name, string Code);
public record DivisionDto(Guid Id, Guid CountryId, string Name);
public record DistrictDto(Guid Id, Guid DivisionId, string Name);
public record UpazilaDto(Guid Id, Guid DistrictId, string Name);
public record UnionDto(Guid Id, Guid UpazilaId, string Name);
public record VillageDto(Guid Id, Guid UnionId, string Name);

public static class LocationMappingExtensions
{
    public static CountryDto ToDto(this Country entity) => new(entity.Id, entity.Name, entity.Code);
    public static DivisionDto ToDto(this Division entity) => new(entity.Id, entity.CountryId, entity.Name);
    public static DistrictDto ToDto(this District entity) => new(entity.Id, entity.DivisionId, entity.Name);
    public static UpazilaDto ToDto(this Upazila entity) => new(entity.Id, entity.DistrictId, entity.Name);
    public static UnionDto ToDto(this Union entity) => new(entity.Id, entity.UpazilaId, entity.Name);
    public static VillageDto ToDto(this Village entity) => new(entity.Id, entity.UnionId, entity.Name);
}
