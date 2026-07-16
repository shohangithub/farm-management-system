using Farm360.Domain.Organizations;
using Farm360.Application.Organizations.Branches.DTOs;

namespace Farm360.Application.Organizations.Branches.DTOs;

public static class BranchMappingExtensions
{
    public static BranchDto ToDto(this Branch branch)
    {
        return new BranchDto(
            branch.Id,
            branch.OrganizationId,
            branch.BranchCode,
            branch.Name,
            branch.ManagerUserId,
            branch.ContactEmail,
            branch.ContactPhone,
            branch.Address,
            branch.Latitude,
            branch.Longitude,
            branch.Status,
            branch.WorkingHours,
            branch.HolidayCalendar,
            branch.IsHeadOffice);
    }

    public static BranchListDto ToListDto(this Branch branch)
    {
        return new BranchListDto(
            branch.Id,
            branch.BranchCode,
            branch.Name,
            branch.ContactEmail,
            branch.ContactPhone,
            branch.Status,
            branch.IsHeadOffice);
    }
}
