using System;
using Farm360.Domain.Common;

namespace Farm360.Domain.Finance;

/// <summary>
/// FarmShareConfig — Aggregate root defining the equity/share capital configuration of a farm.
/// Tracks total authorized shares, owner retained shares, current valuation per share,
/// and whether share purchasing is currently open.
/// </summary>
public sealed class FarmShareConfig : AuditableEntity, IAggregateRoot
{
    public Guid FarmId { get; private set; }
    public int TotalShares { get; private set; }
    public decimal SharePriceBdt { get; private set; }
    public int OwnerShareCount { get; private set; }
    public int AllocatedShareCount { get; private set; }
    public int MinimumPurchaseShares { get; private set; } = 1;
    public bool IsShareSaleOpen { get; private set; } = true;
    public DateTime LastValuationDate { get; private set; }
    public string? ValuationNotes { get; private set; }

    /// <summary>
    /// Total Valuation of the farm = TotalShares × SharePriceBdt.
    /// </summary>
    public decimal TotalValuationBdt => TotalShares * SharePriceBdt;

    /// <summary>
    /// Shares remaining in the pool available for purchase by investors.
    /// Available = TotalShares - OwnerShareCount - AllocatedShareCount.
    /// </summary>
    public int AvailableShareCount => Math.Max(0, TotalShares - OwnerShareCount - AllocatedShareCount);

    /// <summary>
    /// Market value of currently available shares.
    /// </summary>
    public decimal AvailableValuationBdt => AvailableShareCount * SharePriceBdt;

    /// <summary>
    /// Owner's equity value.
    /// </summary>
    public decimal OwnerEquityValueBdt => OwnerShareCount * SharePriceBdt;

    /// <summary>
    /// Owner's ownership percentage.
    /// </summary>
    public decimal OwnerOwnershipPercentage => TotalShares > 0
        ? Math.Round((decimal)OwnerShareCount / TotalShares * 100m, 2)
        : 0m;

    private FarmShareConfig() { } // For EF Core

    public static FarmShareConfig Create(
        Guid tenantId,
        Guid farmId,
        int totalShares,
        decimal sharePriceBdt,
        int ownerShareCount,
        int minimumPurchaseShares = 1,
        bool isShareSaleOpen = true,
        string? valuationNotes = null)
    {
        if (totalShares <= 0)
            throw new ArgumentException("Total shares must be greater than zero.", nameof(totalShares));
        if (sharePriceBdt <= 0)
            throw new ArgumentException("Share price must be greater than zero.", nameof(sharePriceBdt));
        if (ownerShareCount < 0 || ownerShareCount > totalShares)
            throw new ArgumentException("Owner share count must be between 0 and total shares.", nameof(ownerShareCount));
        if (minimumPurchaseShares <= 0)
            throw new ArgumentException("Minimum purchase shares must be at least 1.", nameof(minimumPurchaseShares));

        var config = new FarmShareConfig
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            TotalShares = totalShares,
            SharePriceBdt = sharePriceBdt,
            OwnerShareCount = ownerShareCount,
            AllocatedShareCount = 0,
            MinimumPurchaseShares = minimumPurchaseShares,
            IsShareSaleOpen = isShareSaleOpen,
            LastValuationDate = DateTime.UtcNow,
            ValuationNotes = valuationNotes?.Trim()
        };

        config.SetTenantId(tenantId);
        return config;
    }

    public void UpdateConfiguration(
        int totalShares,
        int ownerShareCount,
        decimal sharePriceBdt,
        int minimumPurchaseShares,
        bool isShareSaleOpen,
        string? notes = null)
    {
        if (totalShares <= 0)
            throw new ArgumentException("Total shares must be greater than zero.", nameof(totalShares));
        if (sharePriceBdt <= 0)
            throw new ArgumentException("Share price must be greater than zero.", nameof(sharePriceBdt));
        if (ownerShareCount < 0)
            throw new ArgumentException("Owner shares cannot be negative.", nameof(ownerShareCount));
        if (ownerShareCount + AllocatedShareCount > totalShares)
            throw new InvalidOperationException($"Cannot set total shares to {totalShares}. Owner shares ({ownerShareCount}) + existing allocated shares ({AllocatedShareCount}) exceed new total.");
        if (minimumPurchaseShares <= 0)
            throw new ArgumentException("Minimum purchase shares must be at least 1.", nameof(minimumPurchaseShares));

        TotalShares = totalShares;
        OwnerShareCount = ownerShareCount;
        SharePriceBdt = sharePriceBdt;
        MinimumPurchaseShares = minimumPurchaseShares;
        IsShareSaleOpen = isShareSaleOpen;
        LastValuationDate = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(notes))
        {
            ValuationNotes = notes.Trim();
        }
    }

    public void UpdateValuation(decimal newSharePriceBdt, string? notes = null)
    {
        if (newSharePriceBdt <= 0)
            throw new ArgumentException("Share price must be greater than zero.", nameof(newSharePriceBdt));

        SharePriceBdt = newSharePriceBdt;
        LastValuationDate = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(notes))
        {
            ValuationNotes = notes.Trim();
        }
    }

    public void AllocateShares(int count)
    {
        if (count <= 0)
            throw new ArgumentException("Share count to allocate must be positive.", nameof(count));
        if (count > AvailableShareCount)
            throw new InvalidOperationException($"Cannot allocate {count} shares. Only {AvailableShareCount} shares are available.");

        AllocatedShareCount += count;
    }

    public void DeallocateShares(int count)
    {
        if (count <= 0)
            throw new ArgumentException("Share count to deallocate must be positive.", nameof(count));
        if (count > AllocatedShareCount)
            throw new InvalidOperationException($"Cannot deallocate {count} shares. Only {AllocatedShareCount} shares are currently allocated.");

        AllocatedShareCount -= count;
    }

    public void ToggleShareSale(bool isOpen)
    {
        IsShareSaleOpen = isOpen;
    }
}
