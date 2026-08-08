using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Application.Analytics.Queries;

public record BreedingAnalyticsDto(
    int TotalConfirmedPregnancies,
    int ExpectedCalvingsNext30Days,
    double ConceptionRatePercentage);

public record MonthlyRevenueExpenseDto(
    int Month,
    int Year,
    decimal TotalRevenueBdt,
    decimal TotalExpenseBdt);

public record FinanceAnalyticsDto(
    IReadOnlyList<MonthlyRevenueExpenseDto> MonthlyData);

public record HealthAnalyticsDto(
    int TotalDeathsLast12Months,
    double VaccinationCompliancePercentage);

public interface IAnalyticsQueryService
{
    Task<BreedingAnalyticsDto> GetBreedingAnalyticsAsync(Guid? farmId, CancellationToken cancellationToken = default);
    Task<FinanceAnalyticsDto> GetFinanceAnalyticsAsync(Guid? farmId, int year, CancellationToken cancellationToken = default);
    Task<HealthAnalyticsDto> GetHealthAnalyticsAsync(Guid? farmId, CancellationToken cancellationToken = default);
}
