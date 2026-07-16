using Farm360.Domain.Farms;

namespace Farm360.Application.Farms.Sheds.DTOs;

public static class ShedMappingExtensions
{
    public static ShedDto ToDto(this Shed shed)
    {
        return new ShedDto(
            shed.Id,
            shed.FarmId,
            shed.ShedNumber,
            shed.ShedName,
            shed.Capacity,
            shed.CurrentOccupancy,
            shed.AnimalType,
            shed.FloorType,
            shed.RoofType,
            shed.HasVentilation,
            shed.HasWaterLine,
            shed.HasFeedLine,
            shed.Status);
    }

    public static ShedListDto ToListDto(this Shed shed)
    {
        return new ShedListDto(
            shed.Id,
            shed.ShedNumber,
            shed.ShedName,
            shed.Capacity,
            shed.CurrentOccupancy,
            shed.AnimalType,
            shed.Status);
    }
}
