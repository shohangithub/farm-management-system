using Farm360.Domain.Organizations.Enums;
using Farm360.Domain.Organizations.ValueObjects;

namespace Farm360.Application.Organizations.Branches.DTOs;

public sealed record BranchDto(
    Guid Id,
    Guid OrganizationId,
    string BranchCode,
    string Name,
    string? ManagerUserId,
    string ContactEmail,
    string? ContactPhone,
    Address? Address,
    double? Latitude,
    double? Longitude,
    BranchStatus Status,
    string? WorkingHours,
    string? HolidayCalendar,
    bool IsHeadOffice);

public sealed record BranchListDto(
    Guid Id,
    string BranchCode,
    string Name,
    string ContactEmail,
    string? ContactPhone,
    BranchStatus Status,
    bool IsHeadOffice);
