using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Analytics.Queries;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Health.Enums;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Persistence.Queries;

public class AnalyticsQueryService : IAnalyticsQueryService
{
    private readonly ApplicationDbContext _context;

    public AnalyticsQueryService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<BreedingAnalyticsDto> GetBreedingAnalyticsAsync(Guid? farmId, CancellationToken cancellationToken = default)
    {
        var breedingQuery = _context.BreedingRecords.AsNoTracking();
        
        if (farmId.HasValue)
        {
            breedingQuery = breedingQuery.Where(br => _context.Animals.Any(a => a.Id == br.AnimalId && a.FarmId == farmId.Value));
        }

        var totalMatings = await breedingQuery.CountAsync(cancellationToken);
        var confirmedPregnancies = await breedingQuery.CountAsync(br => br.IsPregnancyConfirmed, cancellationToken);
        
        var next30Days = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        
        var expectedCalvings = await breedingQuery
            .CountAsync(br => br.ExpectedCalvingDate >= today && br.ExpectedCalvingDate <= next30Days, cancellationToken);

        var conceptionRate = totalMatings == 0 ? 0 : (double)confirmedPregnancies / totalMatings * 100.0;

        return new BreedingAnalyticsDto(
            confirmedPregnancies,
            expectedCalvings,
            Math.Round(conceptionRate, 2));
    }

    public async Task<FinanceAnalyticsDto> GetFinanceAnalyticsAsync(Guid? farmId, int year, CancellationToken cancellationToken = default)
    {
        var financeQuery = _context.FinancialTransactions.AsNoTracking()
            .Where(t => t.TransactionDate.Year == year);

        if (farmId.HasValue)
        {
            financeQuery = financeQuery.Where(t => t.FarmId == farmId.Value);
        }

        var groupedData = await financeQuery
            .GroupBy(t => t.TransactionDate.Month)
            .Select(g => new
            {
                Month = g.Key,
                Revenue = g.Where(t => t.Type == TransactionType.Income).Sum(t => t.AmountBdt),
                Expense = g.Where(t => t.Type == TransactionType.Expense).Sum(t => t.AmountBdt)
            })
            .ToListAsync(cancellationToken);

        var monthlyData = new List<MonthlyRevenueExpenseDto>();
        for (int i = 1; i <= 12; i++)
        {
            var data = groupedData.FirstOrDefault(d => d.Month == i);
            monthlyData.Add(new MonthlyRevenueExpenseDto(
                i,
                year,
                data?.Revenue ?? 0,
                data?.Expense ?? 0));
        }

        return new FinanceAnalyticsDto(monthlyData);
    }

    public async Task<HealthAnalyticsDto> GetHealthAnalyticsAsync(Guid? farmId, CancellationToken cancellationToken = default)
    {
        var oneYearAgo = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1));
        
        var mortalityQuery = _context.MortalityRecords.AsNoTracking()
            .Where(m => m.DeathDate >= oneYearAgo);

        if (farmId.HasValue)
        {
            mortalityQuery = mortalityQuery.Where(m => _context.Animals.Any(a => a.Id == m.AnimalId && a.FarmId == farmId.Value));
        }

        var deaths = await mortalityQuery.CountAsync(cancellationToken);

        // Calculate vaccination compliance (e.g. % of active animals vaccinated in the last 6 months)
        var sixMonthsAgo = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-6));
        
        var animalsQuery = _context.Animals.AsNoTracking();
        if (farmId.HasValue)
        {
            animalsQuery = animalsQuery.Where(a => a.FarmId == farmId.Value);
        }
        
        var totalActiveAnimals = await animalsQuery.CountAsync(cancellationToken);
        
        int vaccinatedAnimals = 0;
        if (totalActiveAnimals > 0)
        {
            var recentlyVaccinatedAnimalIds = await _context.VaccinationEvents
                .Where(v => v.Status == VaccinationStatus.Completed && v.AdministeredDate >= sixMonthsAgo)
                .Select(v => v.AnimalId)
                .Distinct()
                .ToListAsync(cancellationToken);
                
            vaccinatedAnimals = recentlyVaccinatedAnimalIds.Count(id => 
                _context.Animals.Any(a => a.Id == id && (!farmId.HasValue || a.FarmId == farmId.Value)));
        }

        var compliance = totalActiveAnimals == 0 ? 0 : (double)vaccinatedAnimals / totalActiveAnimals * 100.0;

        return new HealthAnalyticsDto(
            deaths,
            Math.Round(compliance, 2));
    }
}
