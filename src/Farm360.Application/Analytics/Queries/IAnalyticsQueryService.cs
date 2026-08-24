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

public record HerdCompositionDto(
    Dictionary<string, int> BySpecies,
    Dictionary<string, int> ByBreed,
    Dictionary<string, int> BySex,
    Dictionary<string, int> ByStatus
);

public record AdgTrendPointDto(string Label, double AdgValue);
public record AdgTrendDto(string BatchId, string BatchName, IReadOnlyList<AdgTrendPointDto> DataPoints);

public record FeedCostTrendPointDto(string Label, decimal CostPerAnimal);
public record FeedCostTrendDto(string GroupName, IReadOnlyList<FeedCostTrendPointDto> DataPoints);

public record VaccinationComplianceDto(int Completed, int Due, int Overdue);

public record FarmSummaryCardDto(Guid FarmId, string FarmName, int AnimalCount, int SickCount, decimal MonthlyRevenue);

public record ActivityFeedItemDto(Guid Id, string ActionType, string EntityName, string Description, string UserName, DateTime Timestamp);

public interface IAnalyticsQueryService
{
    Task<BreedingAnalyticsDto> GetBreedingAnalyticsAsync(Guid? farmId, CancellationToken cancellationToken = default);
    Task<FinanceAnalyticsDto> GetFinanceAnalyticsAsync(Guid? farmId, int year, CancellationToken cancellationToken = default);
    Task<HealthAnalyticsDto> GetHealthAnalyticsAsync(Guid? farmId, CancellationToken cancellationToken = default);
    
    Task<HerdCompositionDto> GetHerdCompositionAsync(Guid? farmId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdgTrendDto>> GetAdgTrendsAsync(Guid? farmId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FeedCostTrendDto>> GetFeedCostTrendsAsync(Guid? farmId, CancellationToken cancellationToken = default);
    Task<VaccinationComplianceDto> GetVaccinationComplianceAsync(Guid? farmId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FarmSummaryCardDto>> GetFarmSummaryCardsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ActivityFeedItemDto>> GetRecentActivityFeedAsync(Guid? farmId, int count = 20, CancellationToken cancellationToken = default);
}
