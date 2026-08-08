using System.Collections.Generic;

namespace Farm360.Application.Dashboard.DTOs;

public sealed record ExecutiveDashboardDto(
    int TotalAnimals,
    int SickAnimals,
    int FeedLowStockCount,
    decimal CurrentMonthIncome,
    decimal CurrentMonthExpense,
    int BirthsThisMonth,
    int DeathsThisMonth,
    int DueVaccinations,
    int PregnantAnimals,
    IReadOnlyList<ActionableInsightDto> ActionableInsights
);
