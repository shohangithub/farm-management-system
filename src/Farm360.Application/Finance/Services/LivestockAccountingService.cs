using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Repositories;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;

namespace Farm360.Application.Finance.Services;

/// <summary>
/// Books an animal's life cycle: purchase (Livestock asset at cost), sale (revenue + cost of the
/// animal released from the asset), slaughter, and death (mortality loss).
/// </summary>
/// <remarks>
/// Every posting is keyed on the animal id, so calling any method again — after a price
/// correction, a re-published event, or from the ledger repair tool — revises instead of duplicating.
/// Nothing here saves; callers commit with their own unit of work.
/// </remarks>
public interface ILivestockAccountingService
{
    /// <summary>Posts or revises the purchase (Dr Livestock, Cr Cash). Born-on-farm animals have no cost.</summary>
    Task SyncPurchaseAsync(Animal animal, CancellationToken cancellationToken = default);

    /// <summary>Posts sale revenue and releases the animal's book cost, or the loss on death/slaughter.</summary>
    Task SyncDisposalAsync(Animal animal, CancellationToken cancellationToken = default);

    /// <summary>The animal's capitalised purchase cost as it stands in the books.</summary>
    Task<decimal> BookCostAsync(Animal animal, CancellationToken cancellationToken = default);
}

public sealed class LivestockAccountingService : ILivestockAccountingService
{
    private const string Module = "Livestock";

    private readonly ILedgerPostingService _posting;
    private readonly IFinancialTransactionRepository _transactions;

    public LivestockAccountingService(ILedgerPostingService posting, IFinancialTransactionRepository transactions)
    {
        _posting = posting;
        _transactions = transactions;
    }

    public async Task SyncPurchaseAsync(Animal animal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(animal);
        await AdoptLegacyPurchaseAsync(animal, cancellationToken);

        var price = animal.AcquisitionType == AcquisitionType.BornOnFarm ? 0m : animal.AcquisitionPriceBdt ?? 0m;
        var tag = animal.Tag.TagId;

        await _posting.PostAsync(new LedgerPosting(
            animal.TenantId,
            animal.FarmId,
            TransactionCategory.AnimalPurchase,
            price,
            animal.AcquisitionDate.ToDateTime(TimeOnly.MinValue),
            animal.Id,
            Module,
            $"Animal Purchase - Tag: {tag}",
            Notes: $"Acquisition cost for animal {tag} (capitalised as livestock asset)",
            ReferenceId: tag,
            AnimalId: animal.Id,
            BatchId: animal.BatchId), cancellationToken);

        // A price correction after the animal has left the farm must also restate the cost released.
        if (animal.Status is AnimalStatus.Sold or AnimalStatus.Dead or AnimalStatus.Slaughtered)
            await SyncDisposalAsync(animal, cancellationToken);
    }

    public async Task SyncDisposalAsync(Animal animal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(animal);
        var tag = animal.Tag.TagId;
        var cost = await BookCostAsync(animal, cancellationToken);

        if (animal.Status == AnimalStatus.Sold && animal.SaleDate is { } saleDate)
        {
            var date = saleDate.ToDateTime(TimeOnly.MinValue);
            var buyer = string.IsNullOrWhiteSpace(animal.BuyerName) ? "Buyer" : animal.BuyerName;

            await AdoptLegacySaleAsync(animal, cancellationToken);
            await _posting.PostAsync(new LedgerPosting(
                animal.TenantId, animal.FarmId, TransactionCategory.AnimalSale,
                animal.SalePriceBdt ?? 0m, date, animal.Id, Module,
                $"Sale of Animal {tag} to {buyer}",
                Notes: $"Sold to {buyer}" + (animal.SaleWeightKg is { } kg ? $" at {kg:0.#} kg" : string.Empty),
                ReferenceId: tag, AnimalId: animal.Id, BatchId: animal.BatchId), cancellationToken);

            await _posting.PostAsync(new LedgerPosting(
                animal.TenantId, animal.FarmId, TransactionCategory.CostOfAnimalsSold,
                cost, date, animal.Id, Module,
                $"Cost of animal sold - Tag: {tag}",
                Settlement: SettlementAccount.Livestock,
                Notes: "Purchase cost released from livestock asset on sale",
                ReferenceId: tag, AnimalId: animal.Id, BatchId: animal.BatchId), cancellationToken);

            await _posting.ReverseAsync(animal.FarmId, animal.Id, TransactionCategory.LivestockLoss, cancellationToken);
            return;
        }

        if (animal.Status is AnimalStatus.Dead or AnimalStatus.Slaughtered)
        {
            var date = (animal.DeathDate ?? DateOnly.FromDateTime(animal.ModifiedAtUtc ?? DateTime.UtcNow)).ToDateTime(TimeOnly.MinValue);

            // Slaughter for meat is a disposal at cost like a sale; a death is a loss.
            var slaughtered = animal.Status == AnimalStatus.Slaughtered || animal.DisposalReason == DisposalReason.Slaughter;
            var category = slaughtered ? TransactionCategory.CostOfAnimalsSold : TransactionCategory.LivestockLoss;
            var other = slaughtered ? TransactionCategory.LivestockLoss : TransactionCategory.CostOfAnimalsSold;

            await _posting.PostAsync(new LedgerPosting(
                animal.TenantId, animal.FarmId, category, cost, date, animal.Id, Module,
                slaughtered ? $"Animal slaughtered - Tag: {tag}" : $"Mortality loss - Tag: {tag} ({animal.DisposalReason})",
                Settlement: SettlementAccount.Livestock,
                Notes: "Purchase cost written off from livestock asset",
                ReferenceId: tag, AnimalId: animal.Id, BatchId: animal.BatchId), cancellationToken);

            await _posting.ReverseAsync(animal.FarmId, animal.Id, other, cancellationToken);
            return;
        }

        // Back in the herd (e.g. a disposal was corrected): nothing should be released.
        await _posting.ReverseAsync(animal.FarmId, animal.Id, TransactionCategory.CostOfAnimalsSold, cancellationToken);
        await _posting.ReverseAsync(animal.FarmId, animal.Id, TransactionCategory.LivestockLoss, cancellationToken);
    }

    public async Task<decimal> BookCostAsync(Animal animal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(animal);
        var purchase = await _transactions.FindBySourceAsync(animal.FarmId, animal.Id, TransactionCategory.AnimalPurchase, cancellationToken)
                       ?? await _transactions.GetAnimalPurchaseTransactionAsync(animal.Id, cancellationToken);
        if (purchase is not null)
            return purchase.AmountBdt;

        return animal.AcquisitionType == AcquisitionType.BornOnFarm ? 0m : animal.AcquisitionPriceBdt ?? 0m;
    }

    /// <summary>Links a purchase posted before source tracking existed, so it is revised, not duplicated.</summary>
    private async Task AdoptLegacyPurchaseAsync(Animal animal, CancellationToken cancellationToken)
    {
        if (await _transactions.FindBySourceAsync(animal.FarmId, animal.Id, TransactionCategory.AnimalPurchase, cancellationToken) is not null)
            return;

        var legacy = await _transactions.GetAnimalPurchaseTransactionAsync(animal.Id, cancellationToken);
        if (legacy is not null && legacy.SourceDocumentId is null)
        {
            legacy.LinkSource(animal.Id, Module);
            await _transactions.UpdateAsync(legacy, cancellationToken);
        }
    }

    private async Task AdoptLegacySaleAsync(Animal animal, CancellationToken cancellationToken)
    {
        if (await _transactions.FindBySourceAsync(animal.FarmId, animal.Id, TransactionCategory.AnimalSale, cancellationToken) is not null)
            return;

        var legacy = (await _transactions.GetByAnimalIdAsync(animal.Id, cancellationToken))
            .FirstOrDefault(t => t.Category == TransactionCategory.AnimalSale && t.SourceDocumentId is null && t.IsAutomated);
        if (legacy is not null)
        {
            legacy.LinkSource(animal.Id, Module);
            await _transactions.UpdateAsync(legacy, cancellationToken);
        }
    }
}
