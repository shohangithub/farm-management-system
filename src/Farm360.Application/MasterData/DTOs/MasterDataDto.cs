using Farm360.Domain.MasterData;

namespace Farm360.Application.MasterData.DTOs;

public record MasterDataDto(
    Guid Id,
    int Type,
    string Name,
    string Code,
    string? Description,
    int DisplayOrder,
    bool IsActive);

public static class MasterDataMappingExtensions
{
    public static MasterDataDto ToDto(this MasterDataEntry entry)
    {
        return new MasterDataDto(
            entry.Id,
            (int)entry.Type,
            entry.Name,
            entry.Code,
            entry.Description,
            entry.DisplayOrder,
            entry.IsActive);
    }
}
