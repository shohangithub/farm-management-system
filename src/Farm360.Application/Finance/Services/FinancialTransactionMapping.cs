using Farm360.Contracts.Finance;
using Farm360.Domain.Finance;

namespace Farm360.Application.Finance.Services;

public static class FinancialTransactionMapping
{
    public static FinancialTransactionDto ToDto(this FinancialTransaction t)
    {
        System.ArgumentNullException.ThrowIfNull(t);
        var head = AccountChart.For(t.Category);
        return new FinancialTransactionDto(
            t.Id,
            t.FarmId,
            t.Type.ToString(),
            t.Category.ToString(),
            t.AmountBdt,
            t.TransactionDate,
            t.Description,
            t.ReferenceId,
            t.Notes,
            t.AnimalId,
            t.BatchId,
            t.ShedId,
            t.CreatedAtUtc,
            t.IsAutomated,
            t.SourceModule)
        {
            Settlement = t.Settlement.ToString(),
            AffectsCash = t.AffectsCash,
            AccountCode = head.Code,
            AccountGroup = head.Group,
            AccountClass = head.Class.ToString(),
        };
    }
}
