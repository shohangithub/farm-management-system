using Farm360.Domain.Farms;

namespace Farm360.Application.Farms.DTOs;

public static class FarmMappingExtensions
{
    public static FarmDto ToDto(this Farm farm)
    {
        return new FarmDto(
            farm.Id,
            farm.BranchId,
            farm.FarmCode,
            farm.FarmName,
            farm.Type,
            farm.FarmSize,
            farm.LandArea,
            farm.Latitude,
            farm.Longitude,
            farm.MapPolygon,
            farm.Capacity,
            farm.CurrentAnimalCount,
            farm.OwnerId,
            farm.ManagerId,
            farm.Status,
            farm.Description);
    }

    public static FarmListDto ToListDto(this Farm farm)
    {
        return new FarmListDto(
            farm.Id,
            farm.FarmCode,
            farm.FarmName,
            farm.Type,
            farm.CurrentAnimalCount,
            farm.Capacity,
            farm.Status);
    }
}
