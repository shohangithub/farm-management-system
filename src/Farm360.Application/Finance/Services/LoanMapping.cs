using Farm360.Contracts.Finance;
using Farm360.Domain.Finance;

namespace Farm360.Application.Finance.Services;

public static class LoanMapping
{
    public static LoanRecordDto ToDto(this LoanRecord loan)
    {
        System.ArgumentNullException.ThrowIfNull(loan);
        return new LoanRecordDto(
            loan.Id,
            loan.FarmId,
            loan.LenderName,
            loan.PrincipalAmountBdt,
            loan.InterestRatePercent,
            loan.DisbursementDate,
            loan.Schedule.ToString(),
            loan.TotalRepaidBdt,
            loan.OutstandingBalanceBdt,
            loan.RepaymentProgressPercent,
            loan.Notes,
            loan.IsActive,
            loan.CreatedAtUtc)
        {
            TotalInterestBdt = loan.TotalInterestBdt,
            TotalPayableBdt = loan.TotalPayableBdt,
            PrincipalRepaidBdt = loan.PrincipalRepaidBdt,
            InterestPaidBdt = loan.InterestPaidBdt,
            OutstandingPrincipalBdt = loan.OutstandingPrincipalBdt,
        };
    }
}
