using Farm360.Domain.Intelligence.Enums;
using System;

namespace Farm360.Application.Dashboard.DTOs;

public sealed record ActionableInsightDto(
    Guid Id,
    Guid FarmId,
    Guid? AnimalId,
    Guid? BatchId,
    InsightType Type,
    InsightSeverity Severity,
    string Title,
    string Message,
    string? ActionData,
    bool IsRead,
    DateTime CreatedOnUtc
);
