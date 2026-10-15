using System;
using Farm360.Domain.Common;

namespace Farm360.Domain.Finance.Accounting;

/// <summary>A farm's accounting period settings, used by every financial statement.</summary>
public sealed class FarmAccountingSettings : AuditableEntity, IAggregateRoot
{
    public Guid FarmId { get; private set; }

    /// <summary>Month (1–12) the fiscal year starts in. Splits retained earnings b/f from the year's profit.</summary>
    public int FiscalYearStartMonth { get; private set; } = FiscalYear.DefaultStartMonth;

    /// <summary>
    /// Date of the last audited balance sheet the farm brings in. Postings on or before it are summarised
    /// by the opening entries; everything after it is calculated. Empty when the whole history is in Farm360.
    /// </summary>
    public DateOnly? BooksStartDate { get; private set; }

    private FarmAccountingSettings() { } // EF Core

    private FarmAccountingSettings(Guid tenantId, Guid farmId) : base(Guid.NewGuid(), tenantId)
    {
        FarmId = farmId;
    }

    public static FarmAccountingSettings Create(Guid tenantId, Guid farmId, int fiscalYearStartMonth, DateOnly? booksStartDate)
    {
        var settings = new FarmAccountingSettings(tenantId, farmId);
        settings.Update(fiscalYearStartMonth, booksStartDate);
        return settings;
    }

    public void Update(int fiscalYearStartMonth, DateOnly? booksStartDate)
    {
        if (fiscalYearStartMonth is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(fiscalYearStartMonth), "The fiscal year must start in a month between 1 and 12.");

        FiscalYearStartMonth = fiscalYearStartMonth;
        BooksStartDate = booksStartDate;
    }
}
