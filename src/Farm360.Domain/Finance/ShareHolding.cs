using System;
using Farm360.Domain.Common;

namespace Farm360.Domain.Finance;

/// <summary>
/// ShareHolding — Aggregate root representing an investor's ownership shares in a specific farm.
/// Tracks current number of shares held, weighted average acquisition price, and total invested capital.
/// </summary>
public sealed class ShareHolding : AuditableEntity, IAggregateRoot
{
    public Guid InvestorId { get; private set; }
    public Guid FarmId { get; private set; }
    public int ShareCount { get; private set; }
    public decimal AveragePurchasePriceBdt { get; private set; }
    public decimal TotalInvestedBdt { get; private set; }
    public string? CertificateNumber { get; private set; }
    public string? Notes { get; private set; }
    public bool IsActive { get; private set; } = true;

    private ShareHolding() { } // For EF Core

    public static ShareHolding Create(
        Guid tenantId,
        Guid investorId,
        Guid farmId,
        int initialShareCount,
        decimal pricePerShareBdt,
        string? certificateNumber = null,
        string? notes = null)
    {
        if (initialShareCount <= 0)
            throw new ArgumentException("Initial share count must be greater than zero.", nameof(initialShareCount));
        if (pricePerShareBdt <= 0)
            throw new ArgumentException("Price per share must be greater than zero.", nameof(pricePerShareBdt));

        var totalCost = initialShareCount * pricePerShareBdt;

        var holding = new ShareHolding
        {
            Id = Guid.NewGuid(),
            InvestorId = investorId,
            FarmId = farmId,
            ShareCount = initialShareCount,
            AveragePurchasePriceBdt = pricePerShareBdt,
            TotalInvestedBdt = totalCost,
            CertificateNumber = string.IsNullOrWhiteSpace(certificateNumber)
                ? $"FSH-{DateTime.UtcNow:yyyyMM}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}"
                : certificateNumber.Trim(),
            Notes = notes?.Trim(),
            IsActive = true
        };

        holding.SetTenantId(tenantId);
        return holding;
    }

    /// <summary>
    /// Adds more shares to the holding and recomputes the weighted average acquisition price.
    /// </summary>
    public void AddShares(int count, decimal pricePerShareBdt)
    {
        if (count <= 0)
            throw new ArgumentException("Share count must be greater than zero.", nameof(count));
        if (pricePerShareBdt <= 0)
            throw new ArgumentException("Price per share must be greater than zero.", nameof(pricePerShareBdt));

        var currentTotalCost = ShareCount * AveragePurchasePriceBdt;
        var addedCost = count * pricePerShareBdt;
        var newTotalShares = ShareCount + count;

        AveragePurchasePriceBdt = Math.Round((currentTotalCost + addedCost) / newTotalShares, 2);
        ShareCount = newTotalShares;
        TotalInvestedBdt += addedCost;
        IsActive = true;
    }

    /// <summary>
    /// Removes shares when sold or transferred, reducing invested capital proportionally.
    /// Returns the realized cost basis of the removed shares.
    /// </summary>
    public decimal RemoveShares(int count)
    {
        if (count <= 0)
            throw new ArgumentException("Share count must be greater than zero.", nameof(count));
        if (count > ShareCount)
            throw new InvalidOperationException($"Cannot remove {count} shares. Only {ShareCount} shares are currently held.");

        var costBasis = Math.Round(count * AveragePurchasePriceBdt, 2);

        ShareCount -= count;
        TotalInvestedBdt = Math.Max(0, TotalInvestedBdt - costBasis);

        if (ShareCount == 0)
        {
            IsActive = false;
        }

        return costBasis;
    }

    public void UpdateNotes(string? notes)
    {
        Notes = notes?.Trim();
    }
}
