using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Repositories;
using Farm360.Application.Intelligence.Services;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Repositories;
using Farm360.Contracts.Finance;
using Farm360.Application.Finance.Services;
using MediatR;

namespace Farm360.Application.Finance.Queries;

public record GetBatchPnLReportQuery(Guid FarmId, Guid BatchId) : IRequest<BatchPnLReportDto>;

/// <summary>
/// Batch P&amp;L: realised revenue against the full cost of every animal in the batch, plus the
/// value of the animals still on hand so an unsold batch does not read as a pure loss.
/// </summary>
public class GetBatchPnLReportQueryHandler : IRequestHandler<GetBatchPnLReportQuery, BatchPnLReportDto>
{
    private const int PageSize = 1000;

    private readonly IFinancialTransactionRepository _transactionRepository;
    private readonly IAnimalCostLedgerRepository _ledgerRepository;
    private readonly IAnimalRepository _animalRepository;
    private readonly IAnimalValuationService _valuation;
    private readonly Farm360.Application.Intelligence.Pricing.IAnimalPriceResolver _prices;

    public GetBatchPnLReportQueryHandler(
        IFinancialTransactionRepository transactionRepository,
        IAnimalCostLedgerRepository ledgerRepository,
        IAnimalRepository animalRepository,
        IAnimalValuationService valuation,
        Farm360.Application.Intelligence.Pricing.IAnimalPriceResolver prices)
    {
        _transactionRepository = transactionRepository;
        _ledgerRepository = ledgerRepository;
        _animalRepository = animalRepository;
        _valuation = valuation;
        _prices = prices;
    }

    public async Task<BatchPnLReportDto> Handle(GetBatchPnLReportQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Every animal in the batch — not only those that happen to have a cost ledger.
        var animals = await LoadBatchAnimalsAsync(request.FarmId, request.BatchId, cancellationToken);
        var ledgers = (await _ledgerRepository.GetByBatchIdAsync(request.FarmId, request.BatchId, cancellationToken))
            .GroupBy(l => l.AnimalId)
            .ToDictionary(g => g.Key, g => g.First());
        var batchAnimals = animals.Select(a => a.Id).ToHashSet();

        // 2. Costs: ledger total per animal; purchase price when an animal has no ledger yet.
        //    On-hand animals are valued with the app's one price resolver (own sales, breed price, farm rule).
        var prices = animals.Any(_valuation.IsOnHand)
            ? await _prices.ForFarmAsync(request.FarmId, null, cancellationToken)
            : null;
        decimal totalCost = 0m;
        decimal unrealisedValue = 0m;
        var onHand = 0;
        var unweighed = 0;
        decimal unweighedValue = 0m;
        foreach (var animal in animals)
        {
            ledgers.TryGetValue(animal.Id, out var ledger);
            var cost = _valuation.CostToDate(animal, ledger).TotalBdt;
            totalCost += cost;

            if (_valuation.IsOnHand(animal))
            {
                onHand++;
                var price = prices?.For(animal);
                var valuedAtCost = price?.ValuedAtCost ?? _valuation.Policy == Farm360.Domain.BusinessRules.AnimalValuationPolicy.CostBasis;
                if (!valuedAtCost && animal.LatestWeightKg is not > 0m)
                {
                    // Not weighed yet: a weight-based value would be 0, so carry the animal at what
                    // it was bought for. Weighed animals are valued at market by weight, which never
                    // adds the feed and care spent, so neither does this.
                    var atCost = _valuation.PurchaseCostBdt(animal, ledger);
                    unweighed++;
                    unweighedValue += atCost;
                    unrealisedValue += atCost;
                    continue;
                }

                unrealisedValue += price is null
                    ? _valuation.CurrentValue(animal, ledger, cost)
                    : _valuation.CurrentValue(animal, ledger, cost, price);
            }
        }

        // 3. Revenue: sales and other revenue booked to the batch or to any of its animals. Only
        //    P&L revenue counts — investor capital or a loan tagged to a batch is not batch income.
        var transactions = await _transactionRepository.GetAllByFarmIdAsync(request.FarmId, cancellationToken);
        var totalIncome = AccountChart.Revenue(transactions.Where(t =>
            t.BatchId == request.BatchId || (t.AnimalId.HasValue && batchAnimals.Contains(t.AnimalId.Value))));

        // 4. Realised vs estimated. Ratios are 0–1 fractions.
        var realisedProfit = totalIncome - totalCost;
        var estimatedProfit = totalIncome + unrealisedValue - totalCost;
        var realisedRoi = totalCost > 0 ? realisedProfit / totalCost : 0m;
        var estimatedRoi = totalCost > 0 ? estimatedProfit / totalCost : 0m;

        return new BatchPnLReportDto(
            request.BatchId,
            request.FarmId,
            Math.Round(totalIncome, 2),
            Math.Round(totalCost, 2),
            Math.Round(realisedProfit, 2),
            Math.Round(realisedRoi, 4),
            animals.Count,
            onHand,
            Math.Round(unrealisedValue, 2),
            Math.Round(estimatedProfit, 2),
            Math.Round(estimatedRoi, 4),
            unweighed,
            Math.Round(unweighedValue, 2));
    }

    private async Task<List<Animal>> LoadBatchAnimalsAsync(Guid farmId, Guid batchId, CancellationToken cancellationToken)
    {
        var animals = new List<Animal>();
        var page = 1;
        while (true)
        {
            var (items, total) = await _animalRepository.GetPagedAsync(
                pageNumber: page,
                pageSize: PageSize,
                farmId: farmId,
                batchId: batchId,
                cancellationToken: cancellationToken);

            animals.AddRange(items);
            if (items.Count == 0 || animals.Count >= total)
            {
                return animals;
            }

            page++;
        }
    }
}
